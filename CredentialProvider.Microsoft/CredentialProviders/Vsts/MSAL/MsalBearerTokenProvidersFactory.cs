// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Collections.Generic;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal class MsalBearerTokenProvidersFactory : IBearerTokenProvidersFactory
    {
        private readonly ILogger logger;
        private readonly IMsalTokenProviderFactory msalTokenProviderFactory;

        public MsalBearerTokenProvidersFactory(ILogger logger, IMsalTokenProviderFactory msalTokenProviderFactory)
        {
            this.msalTokenProviderFactory = msalTokenProviderFactory;
            this.logger = logger;
        }

        public IEnumerable<IBearerTokenProvider> Get(string authority)
        {
            var options = EnvUtil.MsalAllowBrokerEnabled()
                ? new [] {true, false}
                : new [] {false};

            foreach(bool brokerEnabled in options)
            {
                IMsalTokenProvider msalTokenProvider = msalTokenProviderFactory.Get(authority, brokerEnabled, logger);
                yield return new MsalSilentBearerTokenProvider(msalTokenProvider);
                yield return new MsalWindowsIntegratedAuthBearerTokenProvider(msalTokenProvider);
                yield return new MsalUserInterfaceBearerTokenProvider(msalTokenProvider);
                yield return new MsalDeviceCodeFlowBearerTokenProvider(msalTokenProvider);
            }
        }
    }
}
