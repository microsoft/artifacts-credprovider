// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal class MsalTokenProvidersFactory : ITokenProvidersFactory
    {
        private readonly ILogger logger;
        private MsalCacheHelper cache;

        public MsalTokenProvidersFactory(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<IEnumerable<ITokenProvider>> GetAsync(Uri authority)
        {
            if (cache == null && EnvUtil.MsalFileCacheEnabled())
            {
                cache = await MsalCache.GetMsalCacheHelperAsync(EnvUtil.GetMsalCacheLocation(), logger);
            }

            var brokerEnabled = EnvUtil.MsalAllowBrokerEnabled();

            var builder = AzureArtifacts.CreateDefaultBuilder(authority)
                .WithHttpClientFactory(HttpClientFactory.Default)
                .WithLogging(
                    (Microsoft.Identity.Client.LogLevel level, string message, bool containsPii) =>
                    {
                        // We ignore containsPii param because we are passing in enablePiiLogging below.
                        logger.LogTrace("MSAL Log ({level}): {message}", level, message);
                    },
                    enablePiiLogging: EnvUtil.GetLogPIIEnabled()
                )
                .WithBrokerSupport(brokerEnabled, EnvUtil.GetMsalBrokerWindowHandle(), logger);

            // If broker is available on this machine, switch to the broker redirect URI.
            // Otherwise keep http://localhost so system browser auth works (e.g. non-enrolled Mac).
            if (brokerEnabled && builder.IsBrokerAvailable())
            {
                logger.LogTrace("Broker is available; using broker redirect URI.");
                builder.WithBrokerRedirectUri();
            }

            var app = builder.Build();

            cache?.RegisterCache(app.UserTokenCache);

            return MsalTokenProviders.Get(app, logger);
        }
    }
}
