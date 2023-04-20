using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalIntegratedWindowsAuthTokenProvider : ITokenProvider
{
    private readonly IPublicClientApplication app;
    private readonly ILogger logger;

    public MsalIntegratedWindowsAuthTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "MSAL Windows Integrated Authentication";

    public bool IsInteractive => false;

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        return WindowsIntegratedAuth.IsSupported() && tokenRequest.IsWindowsIntegratedAuthEnabled;
    }

    public async Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken)
    {
        try
        {
            string? upn = WindowsIntegratedAuth.GetUserPrincipalName();
            if (upn == null)
            {
                logger.LogTrace(Resources.MsalUserPrincipalNameError, Marshal.GetLastWin32Error());
                return null;
            }

            var result = await app.AcquireTokenByIntegratedWindowsAuth(Constants.AzureDevOpsScopes)
                .WithUsername(upn)
                .ExecuteAsync(cancellationToken);

            return result;
        }
        catch (MsalServiceException e)
        {
            // TODO: Review this, likely want to suppress the wstrust and other known cases
            if (e.ErrorCode.Contains(MsalError.AuthenticationCanceledError))
            {
                return null;
            }

            throw;
        }
    }
}
