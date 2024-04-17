using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;

namespace Microsoft.Artifacts.Authentication;

public class MsalManagedIdentityTokenProvider : ITokenProvider
{
    private readonly ILogger logger;

    public MsalManagedIdentityTokenProvider(ILogger logger)
    {
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
                //TODO move to resource
                logger.LogTrace("invalid client id");
                return null;
            }

            IManagedIdentityApplication mi = ManagedIdentityApplicationBuilder.Create(
                CreateManagedIdentityId(tokenRequest.ClientId!)).Build();

            AuthenticationResult result = await mi.AcquireTokenForManagedIdentity(MsalConstants.AzureDevOpsResource)
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
        // if valid guid assume user assigned client ide
        // otherwise try system
        return Guid.TryParse(clientId, out var id)
            ? ManagedIdentityId.WithUserAssignedClientId(id.ToString())
            : ManagedIdentityId.SystemAssigned;
    }
}
