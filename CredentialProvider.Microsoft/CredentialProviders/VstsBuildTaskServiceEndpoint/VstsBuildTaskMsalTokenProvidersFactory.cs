// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint;

internal class VstsBuildTaskMsalTokenProvidersFactory : ITokenProvidersFactory
{
    private readonly ILogger logger;

    public VstsBuildTaskMsalTokenProvidersFactory(ILogger logger)
    {
        this.logger = logger;
    }

    public Task<IEnumerable<ITokenProvider>> GetAsync(Uri authority)
    {
        var app = AzureArtifacts.CreateDefaultBuilder(authority)
            .WithBroker(EnvUtil.MsalAllowBrokerEnabled(), logger)
            .WithHttpClientFactory(HttpClientFactory.Default)
            .WithLogging(
                (level, message, containsPii) =>
                {
                    // We ignore containsPii param because we are passing in enablePiiLogging below.
                    logger.LogTrace("MSAL Log ({level}): {message}", level, message);
                },
                enablePiiLogging: EnvUtil.GetLogPIIEnabled()
            )
            .Build();

        return Task.FromResult(ConstructTokenProvidersList(app));
    }

    private IEnumerable<ITokenProvider> ConstructTokenProvidersList(IPublicClientApplication app)
    {
        yield return new MsalServicePrincipalTokenProvider(app, logger);
        yield return new MsalManagedIdentityTokenProvider(app, logger);
    }
}
