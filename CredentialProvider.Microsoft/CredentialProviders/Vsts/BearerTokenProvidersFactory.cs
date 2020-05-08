// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Collections.Generic;
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

        public IEnumerable<IBearerTokenProvider> Get(string authority)
        {
            IAdalTokenProvider adalTokenProvider = adalTokenProviderFactory.Get(authority);
            return new IBearerTokenProvider[]
            {
                // Order here is important - providers (potentially) run in this order.
                new AdalCacheBearerTokenProvider(adalTokenProvider),
                new WindowsIntegratedAuthBearerTokenProvider(adalTokenProvider),
                new UserInterfaceBearerTokenProvider(adalTokenProvider, logger),
                new DeviceCodeFlowBearerTokenProvider(adalTokenProvider, logger)
            };
        }
    }
}