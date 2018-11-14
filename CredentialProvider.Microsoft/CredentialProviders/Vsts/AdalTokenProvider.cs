// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
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

        internal AdalTokenProvider(string authority, string resource, string clientId, TokenCache tokenCache)
        {
            this.authority = authority;
            this.resource = resource;
            this.clientId = clientId;
            this.tokenCache = tokenCache;

            // authenticationContext is re-created on each call since the authority can be unexpectedly mutated by another call.
            // e.g. AcquireTokenWithWindowsIntegratedAuth could set it to a specific AAD authority preventing a future AcquireTokenWithDeviceFlowAsync from working for a MSA account.
        }

        public async Task<IAdalToken> AcquireTokenWithDeviceFlowAsync(Func<DeviceCodeResult, Task> deviceCodeHandler, CancellationToken cancellationToken)
        {
            var authenticationContext = new AuthenticationContext(authority, tokenCache);

            var deviceCode = await authenticationContext.AcquireDeviceCodeAsync(resource, clientId);
            cancellationToken.ThrowIfCancellationRequested();

            if (deviceCodeHandler != null)
            {
                await deviceCodeHandler(deviceCode);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var result = await authenticationContext.AcquireTokenByDeviceCodeAsync(deviceCode);
            cancellationToken.ThrowIfCancellationRequested();

            return new AdalToken(result);
        }

        public async Task<IAdalToken> AcquireTokenSilentlyAsync(CancellationToken cancellationToken)
        {
            var authenticationContext = new AuthenticationContext(authority, tokenCache);

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

        public async Task<IAdalToken> AcquireTokenWithUI(CancellationToken cancellationToken)
        {
            var authenticationContext = new AuthenticationContext(authority, tokenCache);

            var parameters =
#if NETFRAMEWORK
                new PlatformParameters(PromptBehavior.Always);
#else
                new PlatformParameters();
#endif

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
        }

        public async Task<IAdalToken> AcquireTokenWithWindowsIntegratedAuth(CancellationToken cancellationToken)
        {
            var authenticationContext = new AuthenticationContext(authority, tokenCache);

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
