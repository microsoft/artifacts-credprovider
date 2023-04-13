// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal class MsalTokenProvider : IMsalTokenProvider
    {
        private readonly Uri authority;
        private readonly string resource;
        private readonly string clientId;
        private readonly bool brokerEnabled;
        private static MsalCacheHelper helper;
        private bool cacheEnabled = false;
        private string cacheLocation;

        internal MsalTokenProvider(Uri authority, string resource, string clientId, bool brokerEnabled, ILogger logger)
        {
            this.authority = authority;
            this.resource = resource;
            this.clientId = clientId;
            this.brokerEnabled = brokerEnabled;
            this.Logger = logger;

            this.cacheEnabled = EnvUtil.MsalFileCacheEnabled();
            this.cacheLocation = this.cacheEnabled ? EnvUtil.GetMsalCacheLocation() : null;
        }

        public string NameSuffix => $"with{(this.brokerEnabled ? "" : "out")} WAM broker.";

        public ILogger Logger { get; private set; }

        private async Task<MsalCacheHelper> GetMsalCacheHelperAsync()
        {
            if (helper == null && this.cacheEnabled)
            {
                this.Logger.Verbose($"Using MSAL cache at `{cacheLocation}`");

                const string cacheFileName = "msal.cache";

                // Copied from GCM https://github.com/GitCredentialManager/git-credential-manager/blob/bdc20d91d325d66647f2837ffb4e2b2fe98d7e70/src/shared/Core/Authentication/MicrosoftAuthentication.cs#L371-L407
                try
                {
                    var storageProps = CreateTokenCacheProperties(useLinuxFallback: false);

                    helper = await MsalCacheHelper.CreateAsync(storageProps);

                    helper.VerifyPersistence();
                }
                catch (MsalCachePersistenceException ex)
                {
                    this.Logger.Warning("warning: cannot persist Microsoft authentication token cache securely!");
                    this.Logger.Verbose(ex.ToString());

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        // On macOS sometimes the Keychain returns the "errSecAuthFailed" error - we don't know why
                        // but it appears to be something to do with not being able to access the keychain.
                        // Locking and unlocking (or restarting) often fixes this.
                        this.Logger.Error(
                            "warning: there is a problem accessing the login Keychain - either manually lock and unlock the " +
                            "login Keychain, or restart the computer to remedy this");
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        // On Linux the SecretService/keyring might not be available so we must fall-back to a plaintext file.
                        this.Logger.Warning("warning: using plain-text fallback token cache");

                        var storageProps = CreateTokenCacheProperties(useLinuxFallback: true);
                        helper = await MsalCacheHelper.CreateAsync(storageProps);
                    }
                }

                StorageCreationProperties CreateTokenCacheProperties(bool useLinuxFallback)
                {
                    var builder = new StorageCreationPropertiesBuilder(cacheFileName, cacheLocation)
                        .WithMacKeyChain("Microsoft.Developer.IdentityService", "MSALCache");

                    if (useLinuxFallback)
                    {
                        builder.WithLinuxUnprotectedFile();
                    }
                    else
                    {
                        // The SecretService/keyring is used on Linux with the following collection name and attributes
                        builder.WithLinuxKeyring(cacheFileName,
                            "default", "MSALCache",
                            new KeyValuePair<string, string>("MsalClientID", "Microsoft.Developer.IdentityService"),
                            new KeyValuePair<string, string>("Microsoft.Developer.IdentityService", "1.0.0.0"));
                    }

                    return builder.Build();
                }
            }

            return helper;
        }

        public async Task<IMsalToken> AcquireTokenWithDeviceFlowAsync(Func<DeviceCodeResult, Task> deviceCodeHandler, CancellationToken cancellationToken, ILogger logger)
        {
            var deviceFlowTimeout = EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(logger);

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(deviceFlowTimeout));
            var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token).Token;
            linkedCancellationToken.ThrowIfCancellationRequested();

            var publicClient = await GetPCAAsync().ConfigureAwait(false);

            var msalBuilder = publicClient.AcquireTokenWithDeviceCode(new string[] { resource }, deviceCodeHandler);
            var result = await msalBuilder.ExecuteAsync(linkedCancellationToken);
            return new MsalToken(result);
        }

        public async Task<IMsalToken> AcquireTokenSilentlyAsync(CancellationToken cancellationToken)
        {
            IPublicClientApplication publicClient = await GetPCAAsync().ConfigureAwait(false);

            var accounts = await publicClient.GetAccountsAsync();

            foreach (var account in accounts)
            {
                this.Logger.Verbose($"Found in cache: {account.HomeAccountId?.TenantId}\\{account.Username}");
            }

            if (Guid.TryParse(this.authority.AbsolutePath.Trim('/'), out Guid authorityTenantId))
            {
                this.Logger.Verbose($"Found tenant `{authorityTenantId}` authority URL: `{this.authority}`");
            }
            else
            {
                this.Logger.Verbose($"Could not determine tenant from authority URL `{this.authority}`");
            }

            string loginHint = EnvUtil.GetMsalLoginHint();

            var applicableAccounts = MsalUtil.GetApplicableAccounts(accounts, authorityTenantId, loginHint);

            if (this.brokerEnabled && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                applicableAccounts.Add((PublicClientApplication.OperatingSystemAccount, PublicClientApplication.OperatingSystemAccount.HomeAccountId.Identifier));
            }

            foreach ((IAccount account, string canonicalName) in applicableAccounts)
            {
                try
                {
                    this.Logger.Verbose($"Attempting to use identity `{canonicalName}`");

                    var result = await publicClient.AcquireTokenSilent(new string[] { resource }, account)
                        .WithAccountTenantId(account)
                        .ExecuteAsync(cancellationToken);

                    return new MsalToken(result);
                }
                catch (MsalUiRequiredException e)
                {
                    this.Logger.Verbose(e.Message);
                }
                catch (MsalServiceException e)
                {
                    this.Logger.Warning(e.Message);
                }
            }

            return null;
        }

        public async Task<IMsalToken> AcquireTokenWithUI(CancellationToken cancellationToken, ILogger logging)
        {
            var deviceFlowTimeout = EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(logging);

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(deviceFlowTimeout));

            var publicClient = await GetPCAAsync(useLocalHost: true).ConfigureAwait(false);

            try
            {
                var result = await publicClient.AcquireTokenInteractive(new string[] { resource })
                    .WithPrompt(Prompt.SelectAccount)
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync(cts.Token);

                return new MsalToken(result);
            }
            catch (MsalServiceException e)
            {
                if (e.ErrorCode.Contains(MsalError.AuthenticationCanceledError))
                {
                    return null;
                }

                throw;
            }
        }

        public async Task<IMsalToken> AcquireTokenWithWindowsIntegratedAuth(CancellationToken cancellationToken)
        {
            var publicClient = await GetPCAAsync().ConfigureAwait(false);

            try
            {
                string upn = WindowsIntegratedAuthUtils.GetUserPrincipalName();
                if (upn == null)
                {
                    return null;
                }

                var result = await publicClient.AcquireTokenByIntegratedWindowsAuth(new string[] { resource })
                    .WithUsername(upn)
                    .ExecuteAsync(cancellationToken);

                return new MsalToken(result);
            }
            catch (MsalServiceException e)
            {
                if (e.ErrorCode.Contains(MsalError.AuthenticationCanceledError))
                {
                    return null;
                }

                throw;
            }
        }

        private async Task<IPublicClientApplication> GetPCAAsync(bool useLocalHost = false)
        {
            var helper = await GetMsalCacheHelperAsync().ConfigureAwait(false);

            var publicClientBuilder = PublicClientApplicationBuilder.Create(this.clientId)
                .WithAuthority(this.authority)
                .WithDefaultRedirectUri()
                .WithLogging(
                    (LogLevel level, string message, bool _containsPii) =>
                    {
                        // We ignore containsPii param because we are passing in enablePiiLogging below.
                        this.Logger.Verbose($"MSAL Log ({level}): {message}");
                    },
                    enablePiiLogging: EnvUtil.GetLogPIIEnabled()
                );

            if (this.brokerEnabled)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    this.Logger.Verbose($"MSAL using WithBrokerPreview");

                    publicClientBuilder
                        .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
                        {
                            Title = "Azure DevOps Artifacts",
                            ListOperatingSystemAccounts = true,
                            MsaPassthrough = true
                        })
                        .WithParentActivityOrWindow(() => GetConsoleOrTerminalWindow());
                }
                else
                {
                    this.Logger.Verbose($"MSAL using WithBroker");
                    publicClientBuilder.WithBroker();
                }
            }

            if (useLocalHost)
            {
                publicClientBuilder.WithRedirectUri("http://localhost");
            }

            var publicClient = publicClientBuilder.Build();
            helper?.RegisterCache(publicClient.UserTokenCache);
            return publicClient;
        }

#region https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/3590
        enum GetAncestorFlags
        {
            GetParent = 1,
            GetRoot = 2,
            /// <summary>
            /// Retrieves the owned root window by walking the chain of parent and owner windows returned by GetParent.
            /// </summary>
            GetRootOwner = 3
        }

        /// <summary>
        /// Retrieves the handle to the ancestor of the specified window.
        /// </summary>
        /// <param name="hwnd">A handle to the window whose ancestor is to be retrieved.
        /// If this parameter is the desktop window, the function returns NULL. </param>
        /// <param name="flags">The ancestor to be retrieved.</param>
        /// <returns>The return value is the handle to the ancestor window.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        public IntPtr GetConsoleOrTerminalWindow()
        {
            IntPtr consoleHandle = GetConsoleWindow();
            IntPtr handle = GetAncestor(consoleHandle, GetAncestorFlags.GetRootOwner);

            return handle;
        }
#endregion
    }
}
