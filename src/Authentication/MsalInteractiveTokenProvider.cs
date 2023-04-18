using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalInteractiveTokenProvider : ITokenProvider
{
    private readonly IPublicClientApplication app;

    public MsalInteractiveTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
    }

    public string Name => "MSAL Interactive";

    public bool IsInteractive => true;

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        // MSAL will use the system browser, this will work on all OS's
        return tokenRequest.IsInteractive && tokenRequest.CanShowDialog;
    }

    public async Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(tokenRequest.InteractiveTimeout);

        try
        {
            var result = await app.AcquireTokenInteractive(Constants.AzureDevOpsScopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync(cts.Token);

            return result;
        }
        catch (MsalServiceException e)
        {
            // TODO: review as this should be from MsalClientException?
            if (e.ErrorCode.Contains(MsalError.AuthenticationCanceledError))
            {
                return null;
            }

            throw;
        }
    }
}
