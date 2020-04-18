// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Collections.Generic;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal class MsalBearerTokenProvidersFactory : IBearerTokenProvidersFactory
    {
        private readonly ILogger logger;
        private readonly IMsalTokenProviderFactory msalTokenProviderFactory;

        public MsalBearerTokenProvidersFactory(ILogger logger,IMsalTokenProviderFactory msalTokenProviderFactory)
        {
            this.msalTokenProviderFactory = msalTokenProviderFactory;
            this.logger = logger;
        }

        public IEnumerable<IBearerTokenProvider> Get(string authority)
        {
            IMsalTokenProvider msalTokenProvider = msalTokenProviderFactory.Get(authority, logger);
            return new IBearerTokenProvider[]
            {
                new MsalCacheBearerTokenProvider(msalTokenProvider),
                new MsalWindowsIntegratedAuthBearerTokenProvider(msalTokenProvider),
                new MsalUserInterfaceBearerTokenProvider(msalTokenProvider),
                new MsalDeviceCodeFlowBearerTokenProvider(msalTokenProvider)
            };
        }
    }
}