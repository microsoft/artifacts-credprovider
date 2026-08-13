// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication
{
    public class MsalFederatedIdentityTokenProvider : ITokenProvider
    {
        public string Name => "MSAL Federated Identity";
        public bool IsInteractive => false;

        private readonly ILogger logger;
        private readonly IAppConfig appConfig;

        public MsalFederatedIdentityTokenProvider(IPublicClientApplication app, ILogger logger)
        {
            this.appConfig = app.AppConfig;
            this.logger = logger;
        }

        public bool CanGetToken(TokenRequest tokenRequest)
        {
            if (string.IsNullOrWhiteSpace(tokenRequest.ClientId)) return false;
            if (string.IsNullOrWhiteSpace(tokenRequest.TenantId)) return false;
            if (string.IsNullOrWhiteSpace(tokenRequest.ClientAssertionFilePath)) return false;
            if (!File.Exists(tokenRequest.ClientAssertionFilePath)) return false;
            return true;
        }

        public async Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!CanGetToken(tokenRequest))
                {
                    logger.LogTrace("InvalidInputs");
                    return null;
                }

                var assertionPath = tokenRequest.ClientAssertionFilePath!;

                var app = ConfidentialClientApplicationBuilder.Create(tokenRequest.ClientId)
                    .WithHttpClientFactory(appConfig.HttpClientFactory)
                    .WithLogging(appConfig.LoggingCallback, appConfig.LogLevel, appConfig.EnablePiiLogging, appConfig.IsDefaultPlatformLoggingEnabled)
                    .WithTenantId(tokenRequest.TenantId)
                    .WithClientAssertion((AssertionRequestOptions _) =>
                    {
                        return Task.FromResult(File.ReadAllText(assertionPath).Trim());
                    })
                    .Build();

                var result = await app.AcquireTokenForClient(MsalConstants.AzureDevOpsScopes)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex.Message);
                return null;
            }
        }
    }
}
