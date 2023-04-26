﻿// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class AdalTokenProvider : IAdalTokenProvider
    {
        private const string NativeClientRedirect = "urn:ietf:wg:oauth:2.0:oob";
        private readonly string authority;
        private readonly string resource;
        private readonly string clientId;
        private readonly TokenCache tokenCache;

        internal AdalTokenProvider(Uri authority, string resource, string clientId, TokenCache tokenCache)
        {
            this.authority = authority.ToString();
            this.resource = resource;
            this.clientId = clientId;
            this.tokenCache = tokenCache;

            // authenticationContext is re-created on each call since the authority can be unexpectedly mutated by another call.
            // e.g. AcquireTokenWithWindowsIntegratedAuth could set it to a specific AAD authority preventing a future AcquireTokenWithDeviceFlowAsync from working for a MSA account.
        }

        public async Task<IAdalToken> AcquireTokenWithDeviceFlowAsync(Func<DeviceCodeResult, Task> deviceCodeHandler, CancellationToken cancellationToken, ILogger logger)
        {
            var authenticationContext = new AuthenticationContext(authority, validateAuthority: true, tokenCache, HttpClientFactory.Default);

            var deviceCode = await authenticationContext.AcquireDeviceCodeAsync(resource, clientId);
            cancellationToken.ThrowIfCancellationRequested();

            if (deviceCodeHandler != null)
            {
                await deviceCodeHandler(deviceCode);
                cancellationToken.ThrowIfCancellationRequested();
            }

            AuthenticationResult result = null;
            var deviceFlowTimeout = EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(logger);
            var task = authenticationContext.AcquireTokenByDeviceCodeAsync(deviceCode);

            if (await Task.WhenAny(task, Task.Delay(deviceFlowTimeout*1000, cancellationToken)) == task)
            {
                result = await task;
            }
            else
            {
                logger.Error(string.Format(Resources.DeviceFlowTimedOut, deviceFlowTimeout));
            }

            return new AdalToken(result);
        }

        public async Task<IAdalToken> AcquireTokenSilentlyAsync(CancellationToken cancellationToken)
        {
            var authenticationContext = new AuthenticationContext(authority, validateAuthority: true, tokenCache, HttpClientFactory.Default);

            try
            {
                var result = await authenticationContext.AcquireTokenSilentAsync(resource, clientId);
                cancellationToken.ThrowIfCancellationRequested();

                return new AdalToken(result);
            }
            catch (AdalSilentTokenAcquisitionException)
            {
                return null;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IAdalToken> AcquireTokenWithUI(CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
#if NETFRAMEWORK
            var authenticationContext = new AuthenticationContext(authority, validateAuthority: true, tokenCache, HttpClientFactory.Default);

            var parameters = new PlatformParameters(PromptBehavior.Always);

            try
            {
                var result = await authenticationContext.AcquireTokenAsync(resource, clientId, new Uri(NativeClientRedirect), parameters);
                cancellationToken.ThrowIfCancellationRequested();

                return new AdalToken(result);
            }
            catch (AdalServiceException e)
            {
                if (e.ErrorCode == AdalError.AuthenticationCanceled)
                {
                    return null;
                }

                throw;
            }
#else
            // no UI in ADAL on netcore
            return null;
#endif
        }

        public async Task<IAdalToken> AcquireTokenWithWindowsIntegratedAuth(CancellationToken cancellationToken)
        {
            var authenticationContext = new AuthenticationContext(authority, validateAuthority: true, tokenCache, HttpClientFactory.Default);

            try
            {
                string upn = WindowsIntegratedAuthUtils.GetUserPrincipalName();
                if (upn == null)
                {
                    return null;
                }

                var result = await authenticationContext.AcquireTokenAsync(resource, clientId, new UserCredential(upn));
                cancellationToken.ThrowIfCancellationRequested();

                return new AdalToken(result);
            }
            catch (AdalServiceException e)
            {
                if (e.ErrorCode == AdalError.AuthenticationCanceled)
                {
                    return null;
                }

                throw;
            }
        }
    }
}
