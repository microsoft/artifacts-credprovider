// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using Microsoft.Extensions.Logging;
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

            var builder = AzureArtifacts.CreateDefaultBuilder(authority)
                .WithHttpClientFactory(HttpClientFactory.Default)
                .WithLogging(
                    (Microsoft.Identity.Client.LogLevel level, string message, bool containsPii) =>
                    {
                        // We ignore containsPii param because we are passing in enablePiiLogging below.
                        logger.LogTrace("MSAL Log ({level}): {message}", level, message);
                    },
                    enablePiiLogging: EnvUtil.GetLogPIIEnabled()
                );

            var app = builder
                .Build();
            var appInteractiveBroker = builder
                .WithBroker(EnvUtil.MsalAllowBrokerEnabled(), EnvUtil.GetMsalBrokerWindowHandle(), logger)
                .Build();

            cache?.RegisterCache(app.UserTokenCache);
            cache?.RegisterCache(appInteractiveBroker.UserTokenCache);

            return MsalTokenProviders.Get(app, appInteractiveBroker, logger);
        }
    }
}
