using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using Microsoft.Extensions.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts;

internal class VstsBuildTaskMsalTokenProvidersFactory : ITokenProvidersFactory
{
    private readonly ILogger logger;

    public VstsBuildTaskMsalTokenProvidersFactory(ILogger logger)
    {
        this.logger = logger;
    }

    public Task<IEnumerable<ITokenProvider>> Get(Uri authority)
    {
        var app = AzureArtifacts.CreateDefaultBuilder(authority)
            .WithBroker(EnvUtil.MsalAllowBrokerEnabled(), logger)
            .WithHttpClientFactory(HttpClientFactory.Default)
            .WithLogging(
                (Microsoft.Identity.Client.LogLevel level, string message, bool containsPii) =>
                {
                    // We ignore containsPii param because we are passing in enablePiiLogging below.
                    logger.LogTrace("MSAL Log ({level}): {message}", level, message);
                },
                enablePiiLogging: EnvUtil.GetLogPIIEnabled()
            )
            .Build();

        return Task.FromResult(MsalTokenProviders.Get(app, logger)
            .Where(x => x.Name == "MSAL Managed Identity" || x.Name == "MSAL Service Principal"));
    }
}
