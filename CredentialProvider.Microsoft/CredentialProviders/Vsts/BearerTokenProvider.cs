// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class BearerTokenProvider : IBearerTokenProvider
    {
        private const string Resource = "499b84ac-1321-427f-aa17-267ca6975798";
        private const string ClientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";

        private readonly ILogger logger;
        private readonly IAdalTokenProviderFactory adalTokenProviderFactory;
        private readonly IAuthUtil authUtil;

        public BearerTokenProvider(ILogger logger)
            : this(logger, new AdalTokenProviderFactory(Resource, ClientId, GetTokenCache(logger)), new AuthUtil(logger))
        {
        }

        public BearerTokenProvider(ILogger logger, IAdalTokenProviderFactory adalTokenProviderFactory, IAuthUtil authUtil)
        {
            this.logger = logger;
            this.adalTokenProviderFactory = adalTokenProviderFactory;
            this.authUtil = authUtil;
        }

        public async Task<string> GetAsync(Uri uri, bool isRetry, bool isNonInteractive, bool canShowDialog, CancellationToken cancellationToken)
        {
            var authority = await authUtil.GetAadAuthorityUriAsync(uri, cancellationToken);
            logger.Verbose(string.Format(Resources.AdalUsingAuthority, authority));

            var adalTokenProvider = adalTokenProviderFactory.Get(authority.ToString());
            cancellationToken.ThrowIfCancellationRequested();

            IAdalToken adalToken;

            // Try to acquire token silently
            if (!isRetry)
            {
                adalToken = await adalTokenProvider.AcquireTokenSilentlyAsync(cancellationToken);
                if (adalToken?.AccessToken != null)
                {
                    logger.Verbose(Resources.AdalAcquireTokenSilentSuccess);
                    return adalToken.AccessToken;
                }
                else
                {
                    logger.Verbose(Resources.AdalAcquireTokenSilentFailed);
                }
            }

            // Interactive flows if allowed
            if (!isNonInteractive)
            {
#if NETFRAMEWORK
                if (canShowDialog && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Try UI prompt
                    adalToken = await adalTokenProvider.AcquireTokenWithUI(cancellationToken);

                    if (adalToken?.AccessToken != null)
                    {
                        return adalToken.AccessToken;
                    }
                }
#endif

                // Try device flow
                adalToken = await adalTokenProvider.AcquireTokenWithDeviceFlowAsync(
                    (DeviceCodeResult deviceCodeResult) =>
                    {
                        logger.Minimal(string.Format(Resources.AdalDeviceFlowRequestedResource, uri.ToString()));
                        logger.Minimal(string.Format(Resources.AdalDeviceFlowMessage, deviceCodeResult.VerificationUrl, deviceCodeResult.UserCode));

                        return Task.CompletedTask;
                    },
                    cancellationToken);

                if (adalToken?.AccessToken != null)
                {
                    logger.Verbose(Resources.AdalAcquireTokenDeviceFlowSuccess);
                    return adalToken.AccessToken;
                }
                else
                {
                    logger.Verbose(Resources.AdalAcquireTokenDeviceFlowFailed);
                }
            }
            else if (isRetry)
            {
                logger.Warning(Resources.CannotRetryWithNonInteractiveFlag);
            }

            logger.Verbose(string.Format(Resources.AdalTokenNotFound, uri.ToString()));
            return null;
        }

        private static TokenCache GetTokenCache(ILogger logger)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.Verbose(Resources.DPAPIUnavailableNonWindows);
                return TokenCache.DefaultShared;
            }

            if (!EnvUtil.AdalFileCacheEnabled())
            {
                logger.Verbose(Resources.AdalFileCacheDisabled);
                return TokenCache.DefaultShared;
            }

            logger.Verbose(string.Format(Resources.AdalFileCacheLocation, EnvUtil.AdalTokenCacheLocation));
            return new AdalFileCache(EnvUtil.AdalTokenCacheLocation);
        }
    }
}
