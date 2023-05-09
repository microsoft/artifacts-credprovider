// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class BearerTokenProvidersFactory : IBearerTokenProvidersFactory
    {
        private readonly ILogger logger;
        private readonly IAdalTokenProviderFactory adalTokenProviderFactory;

        public BearerTokenProvidersFactory(ILogger logger, IAdalTokenProviderFactory adalTokenProviderFactory)
        {
            this.adalTokenProviderFactory = adalTokenProviderFactory;
            this.logger = logger;
        }

        public Task<IEnumerable<ITokenProvider>> GetAsync(Uri authority)
        {
            IAdalTokenProvider adalTokenProvider = adalTokenProviderFactory.Get(authority);
            return Task.FromResult<IEnumerable<ITokenProvider>>(new IBearerTokenProvider[]
            {
                // Order here is important - providers (potentially) run in this order.
                new AdalCacheBearerTokenProvider(adalTokenProvider),
                new WindowsIntegratedAuthBearerTokenProvider(adalTokenProvider),
                new UserInterfaceBearerTokenProvider(adalTokenProvider, logger),
                new DeviceCodeFlowBearerTokenProvider(adalTokenProvider, logger)
            });
        }
    }
}
