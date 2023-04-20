using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalTokenProvidersFactory : ITokenProvidersFactory
{
    private readonly IPublicClientApplication app;
    private readonly ILogger logger;

    public MsalTokenProvidersFactory(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IEnumerable<ITokenProvider> Get(Uri authority)
    {
        // TODO: Would be more useful if MsalSilentTokenProvider enumerated over each account from the outside
        yield return new MsalSilentTokenProvider(app, logger);

        if (WindowsIntegratedAuth.IsSupported())
        {
            yield return new MsalIntegratedWindowsAuthTokenProvider(app, logger);
        }

        yield return new MsalInteractiveTokenProvider(app, logger);
        yield return new MsalDeviceCodeTokenProvider(app, logger);
    }
}
