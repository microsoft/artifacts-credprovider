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
        private readonly IMsalTokenProviderFactory msalTokenProviderFactory;

        public BearerTokenProvidersFactory(ILogger logger, IAdalTokenProviderFactory adalTokenProviderFactory = null, IMsalTokenProviderFactory msalTokenProviderFactory = null)
        {
            this.adalTokenProviderFactory = adalTokenProviderFactory;
            this.msalTokenProviderFactory = msalTokenProviderFactory;
            this.logger = logger;
        }

        public IEnumerable<IBearerTokenProvider> Get(string authority)
        {
            List<IBearerTokenProvider> providers = new List<IBearerTokenProvider>();
            if (msalTokenProviderFactory != null)
            {
                IMsalTokenProvider msalTokenProvider = msalTokenProviderFactory.Get(authority, logger);
                providers.Add(new MsalCacheBearerTokenProvider(msalTokenProvider));
                providers.Add(new MsalWindowsIntegratedAuthBearerTokenProvider(msalTokenProvider));
                providers.Add(new MsalUserInterfaceBearerTokenProvider(msalTokenProvider));
                providers.Add(new MsalDeviceCodeFlowBearerTokenProvider(msalTokenProvider));
            }

            if (adalTokenProviderFactory != null)
            {
                IAdalTokenProvider adalTokenProvider = adalTokenProviderFactory.Get(authority);

                // Order here is important - providers (potentially) run in this order.
                providers.Add(new AdalCacheBearerTokenProvider(adalTokenProvider));
                providers.Add(new WindowsIntegratedAuthBearerTokenProvider(adalTokenProvider));
                providers.Add(new UserInterfaceBearerTokenProvider(adalTokenProvider));
                providers.Add(new DeviceCodeFlowBearerTokenProvider(adalTokenProvider, logger));
            }

            return providers;
        }
    }
}