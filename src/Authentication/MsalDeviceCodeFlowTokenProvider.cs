using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalDeviceCodeFlowTokenProvider : ITokenProvider
{
    private readonly IPublicClientApplication app;
    private readonly ILogger logger;

    public MsalDeviceCodeFlowTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "MSAL Device Code";

    public bool IsInteractive => true;

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        return tokenRequest.IsInteractive;
    }

    public Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(tokenRequest.InteractiveTimeout);

        var result = app.AcquireTokenWithDeviceCode(Constants.AzureDevOpsScopes, tokenRequest.DeviceCodeResultCallback ?? ((DeviceCodeResult deviceCodeResult) =>
            {
                logger.LogInformation(deviceCodeResult.Message);

                return Task.CompletedTask;
            }))
            .ExecuteAsync(cts.Token);

        return result;
    }
}
