using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication
{
    public class MsalServicePrincipalTokenProvider : ITokenProvider
    {
        public string Name => "MSAL Service Principal";

        public bool IsInteractive => false;

        private readonly ILogger logger;
        private readonly IAppConfig appConfig;

        public MsalServicePrincipalTokenProvider(IPublicClientApplication app, ILogger logger)
        {
            this.appConfig = app.AppConfig;
            this.logger = logger;
        }

        public bool CanGetToken(TokenRequest tokenRequest)
        {
            return !string.IsNullOrWhiteSpace(tokenRequest.ClientId)
                && (tokenRequest.ClientCertificate != null || tokenRequest.ClientSecret != null);
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

                var app = ConfidentialClientApplicationBuilder.Create(tokenRequest.ClientId)
                    .WithHttpClientFactory(appConfig.HttpClientFactory)
                    .WithLogging(appConfig.LoggingCallback, appConfig.LogLevel, appConfig.EnablePiiLogging, appConfig.IsDefaultPlatformLoggingEnabled)
                    .WithCertificateOrClientSecret(tokenRequest)
                    .WithTenantId(tokenRequest.TenantId)
                    .Build();

                var result = await app.AcquireTokenForClient(MsalConstants.AzureDevOpsScopes)
                    .ExecuteAsync()
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

    public static class MsalApplicationBuilderExtensions
    {
        public static ConfidentialClientApplicationBuilder WithCertificateOrClientSecret(
            this ConfidentialClientApplicationBuilder builder,
            TokenRequest tokenRequest
        )
        {
            if (tokenRequest.ClientCertificate != null)
            {
                return builder.WithCertificate(tokenRequest.ClientCertificate, sendX5C: true);
            }
            else
            {
                return builder.WithClientSecret(tokenRequest.ClientSecret?.GetSecretString()!);
            }
        }
    }
}
