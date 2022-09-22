// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;
using System.Collections.Generic;
using System.Linq;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal class MsalTokenProvider : IMsalTokenProvider
    {
        private const string NativeClientRedirect = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        private readonly string authority;
        private readonly string resource;
        private readonly string clientId;
        private readonly bool brokerEnabled;
        private static MsalCacheHelper helper;
        private bool cacheEnabled = false;
        private string cacheLocation;

        internal MsalTokenProvider(string authority, string resource, string clientId, bool brokerEnabled, ILogger logger)
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
            // There are options to set up the cache correctly using StorageCreationProperties on other OS's but that will need to be tested
            // for now only support windows
            if (helper == null && this.cacheEnabled && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Logger.Verbose($"Using MSAL cache at `{cacheLocation}`.");

                var fileName = Path.GetFileName(cacheLocation);
                var directory = Path.GetDirectoryName(cacheLocation);

                var builder = new StorageCreationPropertiesBuilder(fileName, directory);
                StorageCreationProperties creationProps = builder.Build();
                helper = await MsalCacheHelper.CreateAsync(creationProps);
            }

            return helper;
        }

        public async Task<IMsalToken> AcquireTokenWithDeviceFlowAsync(Func<DeviceCodeResult, Task> deviceCodeHandler, CancellationToken cancellationToken, ILogger logger)
        {
            var deviceFlowTimeout = EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(logger);

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(deviceFlowTimeout));
            var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token).Token;
            linkedCancellationToken.ThrowIfCancellationRequested();

            var publicClient = await GetPCAAsync(useLocalHost: false).ConfigureAwait(false);

            var msalBuilder = publicClient.AcquireTokenWithDeviceCode(new string[] { resource }, deviceCodeHandler);
            var result = await msalBuilder.ExecuteAsync(linkedCancellationToken);
            return new MsalToken(result);
        }

        internal static List<(IAccount, string, int)> PrioritizeAccounts(IEnumerable<IAccount> accounts, Guid? authorityTenant, string loginHint)
        {
            return accounts
                .Select(account => {
                    int matchScore = 0;
                    if (Guid.TryParse(account.HomeAccountId.TenantId, out Guid accountTenant)) {
                        if (authorityTenant.HasValue)
                        {
                            if (authorityTenant.Value == accountTenant)
                            {
                                matchScore += 1;
                            }
                            // for some reason there are two MSA tenants?
                            else if (authorityTenant.Value == AuthUtil.MsaAuthorityTenant && accountTenant == AuthUtil.MsaAccountTenant)
                            {
                                matchScore += 1;
                            }
                        }
                        else
                        {
                            // if the authority is not provided, that probably means it's not AAD-backed,
                            // but rather it is MSA-backed.
                            if (accountTenant == AuthUtil.MsaAccountTenant)
                            {
                                matchScore += 1;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(loginHint) && 0 <= account.Username.IndexOf(loginHint, StringComparison.Ordinal))
                    {
                        matchScore += 10;
                    }

                    string canonicalName = $"{account.HomeAccountId.TenantId}\\{account.Username}";

                    return (account, canonicalName, matchScore);
                })
                .OrderByDescending( a => a.matchScore)
                .ToList();
        }

        public async Task<IMsalToken> AcquireTokenSilentlyAsync(CancellationToken cancellationToken)
        {
            IPublicClientApplication publicClient = await GetPCAAsync(useLocalHost: false).ConfigureAwait(false);
            var accounts = new List<IAccount>();

            string loginHint = EnvUtil.GetMsalLoginHint();

            accounts.AddRange(await publicClient.GetAccountsAsync());

            if (this.brokerEnabled && accounts.Count == 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                accounts.Add(PublicClientApplication.OperatingSystemAccount);
            }

            Guid? authorityTenant = null;
            try
            {
                authorityTenant = Guid.Parse((new Uri(this.authority)).AbsolutePath.Trim('/'));
                this.Logger.Verbose($"Found tenant `{authorityTenant.Value}` authority URL: `{this.authority}`.");

            }
            catch (Exception e)
            {
                this.Logger.Verbose($"Could not determine tenant from authority URL `{this.authority}`: {e.Message}");
            }

            var sortedAccounts = PrioritizeAccounts(accounts, authorityTenant, loginHint);

            foreach ((IAccount account, string canonicalName, _) in sortedAccounts)
            {
                this.Logger.Verbose($"Found in cache: {canonicalName}");
            }

            foreach ((IAccount account, string canonicalName, _) in sortedAccounts)
            {
                try
                {
                    this.Logger.Verbose($"Attempting to use identity `{canonicalName}`.");
                    var silentBuilder = publicClient.AcquireTokenSilent(new string[] { resource }, account);
                    var result = await silentBuilder.ExecuteAsync(cancellationToken);
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

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(deviceFlowTimeout));
            var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token).Token;
            var publicClient = await GetPCAAsync(useLocalHost: true).ConfigureAwait(false);

            try
            {
                var msalBuilder = publicClient.AcquireTokenInteractive(new string[] { resource });
                msalBuilder.WithPrompt(Prompt.SelectAccount);
                msalBuilder.WithUseEmbeddedWebView(false);
                var result = await msalBuilder.ExecuteAsync(linkedCancellationToken);
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
            var publicClient = await GetPCAAsync(useLocalHost: false).ConfigureAwait(false);

            try
            {
                string upn = WindowsIntegratedAuthUtils.GetUserPrincipalName();
                if (upn == null)
                {
                    return null;
                }

                var builder = publicClient.AcquireTokenByIntegratedWindowsAuth(new string[] { resource });
                builder.WithUsername(upn);
                var result = await builder.ExecuteAsync(cancellationToken);

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

        private async Task<IPublicClientApplication> GetPCAAsync(bool useLocalHost)
        {
            var helper = await GetMsalCacheHelperAsync().ConfigureAwait(false);

            var publicClientBuilder = PublicClientApplicationBuilder.Create(this.clientId)
                .WithAuthority(this.authority)
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
                    publicClientBuilder = publicClientBuilder.WithBrokerPreview();

                    //  needed for WAM
                    publicClientBuilder = publicClientBuilder.WithParentActivityOrWindow(() => GetConsoleOrTerminalWindow());

                    publicClientBuilder = publicClientBuilder.WithWindowsBrokerOptions(new WindowsBrokerOptions() {
                        HeaderText = "Azure DevOps Artifacts",
                        // System.NotImplementedException: The new broker implementation does not yet support Windows account discovery (ListWindowsWorkAndSchoolAccounts option)
                        // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/pull/3602
                        // ListWindowsWorkAndSchoolAccounts = true,
                    });
                }
                else
                {
                    this.Logger.Verbose($"MSAL using WithBroker");
                    publicClientBuilder = publicClientBuilder.WithBroker();
                }
            }

            publicClientBuilder = publicClientBuilder.WithRedirectUri(
                useLocalHost
                    ? "http://localhost"
                    : NativeClientRedirect);

            var publicClient = publicClientBuilder.Build();
            helper?.RegisterCache(publicClient.UserTokenCache);
            return publicClient;
        }

#region https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/WAM
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
            IntPtr handle = GetAncestor(consoleHandle, GetAncestorFlags.GetRootOwner );
            
            return handle;
        }
#endregion
    }
}
