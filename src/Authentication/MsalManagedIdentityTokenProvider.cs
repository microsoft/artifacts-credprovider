using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;

namespace Microsoft.Artifacts.Authentication;

public class MsalManagedIdentityTokenProvider : ITokenProvider
{
    private readonly ILogger logger;
    private readonly IAppConfig appConfig;

    public MsalManagedIdentityTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.appConfig = app.AppConfig;
        this.logger = logger;
    }

    public string Name => "MSAL Managed Identity";

    public bool IsInteractive => false;

    public bool CanGetToken(TokenRequest tokenRequest) => 
        !string.IsNullOrWhiteSpace(tokenRequest.ClientId);

    public async Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tokenRequest.ClientId))
            {
                logger.LogTrace(string.Format(Resources.MsalClientIdError, tokenRequest.ClientId));
                return null;
            }

            IManagedIdentityApplication app = ManagedIdentityApplicationBuilder.Create(CreateManagedIdentityId(tokenRequest.ClientId!))
                .WithHttpClientFactory(appConfig.HttpClientFactory)
                .WithLogging(appConfig.LoggingCallback, appConfig.LogLevel, appConfig.EnablePiiLogging, appConfig.IsDefaultPlatformLoggingEnabled)
                .Build();

            AuthenticationResult result = await app.AcquireTokenForManagedIdentity(MsalConstants.AzureDevOpsResource)
                .ExecuteAsync()
                .ConfigureAwait(false);

            return result;
        } 
        catch (MsalServiceException ex) when (ex.ErrorCode is MsalError.ManagedIdentityRequestFailed)
        {
            logger.LogTrace(ex.Message);
            return null;
        }
        catch (MsalServiceException ex) when (ex.ErrorCode is MsalError.ManagedIdentityUnreachableNetwork)
        {
            logger.LogTrace(ex.Message);
            return null;
        }
    }

    private ManagedIdentityId CreateManagedIdentityId(string clientId)
    {
        return Guid.TryParse(clientId, out var id)
            ? ManagedIdentityId.WithUserAssignedClientId(id.ToString())
            : ManagedIdentityId.SystemAssigned;
    }
}
