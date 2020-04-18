// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal class MsalTokenProvider : IMsalTokenProvider
    {
        private const string NativeClientRedirect = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        private readonly string authority;
        private readonly string resource;
        private readonly string clientId;
        private static MsalCacheHelper helper;
        private bool cacheEnabled = false;
        private string cacheLocation;

        internal MsalTokenProvider(string authority, string resource, string clientId, ILogger logger)
        {
            this.authority = authority;
            this.resource = resource;
            this.clientId = clientId;
            this.Logger = logger;

            this.cacheEnabled = EnvUtil.MsalFileCacheEnabled();
            this.cacheLocation = this.cacheEnabled ? EnvUtil.GetMsalCacheLocation() : null;
        }

        public ILogger Logger { get; private set; }

        private async Task<MsalCacheHelper> GetMsalCacheHelperAsync()
        {
            if (helper == null && this.cacheEnabled)
            {
                var fileName = Path.GetFileName(cacheLocation);
                var directory = Path.GetDirectoryName(cacheLocation);

                var builder = new StorageCreationPropertiesBuilder(fileName, directory, this.clientId);
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

            var publicClient = await GetPCAAsync().ConfigureAwait(false);

            try
            {
                var msalBuilder = publicClient.AcquireTokenWithDeviceCode(new string[] { resource }, deviceCodeHandler);
                var result = await msalBuilder.ExecuteAsync(linkedCancellationToken);
                return new MsalToken(result);
            }
            finally
            {
                var helper = await GetMsalCacheHelperAsync();
                helper?.UnregisterCache(publicClient.UserTokenCache);
            }
        }

        public async Task<IMsalToken> AcquireTokenSilentlyAsync(CancellationToken cancellationToken)
        {
            var publicClient = await GetPCAAsync().ConfigureAwait(false);
            var accounts = await publicClient.GetAccountsAsync();

            try
            {
                foreach (var account in accounts)
                {
                    try
                    {
                        var silentBuilder = publicClient.AcquireTokenSilent(new string[] { resource }, account);
                        var result = await silentBuilder.ExecuteAsync(cancellationToken);
                        return new MsalToken(result);
                    }
                    catch (MsalUiRequiredException)
                    { }
                    catch (MsalServiceException)
                    { }
                }
            }
            finally
            {
                var helper = await GetMsalCacheHelperAsync();
                helper?.UnregisterCache(publicClient.UserTokenCache);
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
            finally
            {
                var helper = await GetMsalCacheHelperAsync();
                helper?.UnregisterCache(publicClient.UserTokenCache);
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

                var builder = publicClient.AcquireTokenByIntegratedWindowsAuth(new string[] { resource});
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
            finally
            {
                var helper = await GetMsalCacheHelperAsync();
                helper?.UnregisterCache(publicClient.UserTokenCache);
            }
       }

        private async Task<IPublicClientApplication> GetPCAAsync(bool useLocalHost = false)
        {
            var helper = await GetMsalCacheHelperAsync().ConfigureAwait(false);

            var publicClientBuilder = PublicClientApplicationBuilder.Create(this.clientId)
                .WithAuthority(this.authority);

            if (useLocalHost)
            {
                publicClientBuilder.WithRedirectUri("http://localhost");
            }
            else
            {
                publicClientBuilder.WithRedirectUri(NativeClientRedirect);
            }

            var publicClient = publicClientBuilder.Build();
            helper?.RegisterCache(publicClient.UserTokenCache);
            return publicClient;
        }
    }
}
