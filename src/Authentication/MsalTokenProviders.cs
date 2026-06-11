// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalTokenProviders
{
    public static IEnumerable<ITokenProvider> Get(IPublicClientApplication app, ILogger logger)
    {
        yield return new MsalServicePrincipalTokenProvider(app, logger);
        yield return new MsalManagedIdentityTokenProvider(app, logger);

        // TODO: Would be more useful if MsalSilentTokenProvider enumerated over each account from the outside
        yield return new MsalSilentTokenProvider(app, logger);

        if (WindowsIntegratedAuth.IsSupported())
        {
            yield return new MsalIntegratedWindowsAuthTokenProvider(app, logger);
        }

        // Interactive auth: if broker is configured on the PCA, MSAL will try broker first
        // and fall back to system browser (http://localhost) automatically.
        yield return new MsalInteractiveTokenProvider(app, logger);

        // Fallback to device code flow as last resort
        yield return new MsalDeviceCodeTokenProvider(app, logger);
    }

    /// <summary>
    /// Obsolete overload kept for backward compatibility. Use <see cref="Get(IPublicClientApplication, ILogger)"/> with a broker-configured PCA instead.
    /// </summary>
    [Obsolete("Use Get(app, logger) with a broker-configured PCA instead. The appInteractiveBroker parameter is no longer used.")]
    public static IEnumerable<ITokenProvider> Get(IPublicClientApplication app, ILogger logger, IPublicClientApplication? appInteractiveBroker)
        => Get(app, logger);
}
