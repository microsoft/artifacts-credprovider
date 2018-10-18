// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class AdalTokenProvider : IAdalTokenProvider
    {
        private const string NativeClientRedirect = "urn:ietf:wg:oauth:2.0:oob";
        private readonly string resource;
        private readonly string clientId;

        private AuthenticationContext authenticationContext;

        internal AdalTokenProvider(string authority, string resource, string clientId, TokenCache tokenCache)
        {
            this.resource = resource;
            this.clientId = clientId;
            this.authenticationContext = new AuthenticationContext(authority, tokenCache);
        }

        public async Task<IAdalToken> AcquireTokenWithDeviceFlowAsync(Func<DeviceCodeResult, Task> deviceCodeHandler, CancellationToken cancellationToken)
        {
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
