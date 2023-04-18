using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalSilentTokenProvider : ITokenProvider
{
    private readonly IPublicClientApplication app;
    private readonly ILogger logger;

    public MsalSilentTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "MSAL Silent";

    public bool IsInteractive => false;

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        // Always run this and rely on MSAL to return a valid token. Previously, AcquireTokenByIntegratedWindowsAuth
        // would return cached tokens based on the user principal name, so this token provider could be skipped. Now,
        // cached and broker accounts and any cached tokens are returned via AcquireTokenSilent. MSAL will ensure any
        // returned token is refreshed as needed to be valid upon return.
        return true;
    }

    public async Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken)
    {
        var accounts = await app.GetAccountsAsync();

        foreach (var account in accounts)
        {
            this.logger.LogTrace($"Found in cache: {account.HomeAccountId?.TenantId}\\{account.Username}");
        }

        var authority = new Uri(app.Authority);

        if (Guid.TryParse(authority.AbsolutePath.Trim('/'), out Guid authorityTenantId))
        {
            this.logger.LogTrace($"Found tenant `{authorityTenantId}` authority URL: `{authority}`");
        }
        else
        {
            this.logger.LogTrace($"Could not determine tenant from authority URL `{authority}`");
        }

        var applicableAccounts = MsalExtensions.GetApplicableAccounts(accounts, authorityTenantId, tokenRequest.LoginHint);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            applicableAccounts.Add((PublicClientApplication.OperatingSystemAccount, PublicClientApplication.OperatingSystemAccount.HomeAccountId.Identifier));
        }

        foreach ((IAccount account, string canonicalName) in applicableAccounts)
        {
            try
            {
                this.logger.LogTrace($"Attempting to use identity `{canonicalName}`");

                var result = await app.AcquireTokenSilent(Constants.AzureDevOpsScopes, account)
                    .WithAccountTenantId(account)
                    .ExecuteAsync(cancellationToken);

                return result;
            }
            catch (MsalUiRequiredException e)
            {
                this.logger.LogTrace(e.Message);
            }
            catch (MsalServiceException e)
            {
                this.logger.LogWarning(e.Message);
            }
        }

        return null;
    }
}
