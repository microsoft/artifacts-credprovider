// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalTokenProviders
{
    public static IEnumerable<ITokenProvider> Get(IPublicClientApplication app, IPublicClientApplication appInteractiveBroker, bool brokerEnabled, ILogger logger)
    {
        yield return new MsalServicePrincipalTokenProvider(app, logger);
        yield return new MsalManagedIdentityTokenProvider(app, logger);

        // TODO: Would be more useful if MsalSilentTokenProvider enumerated over each account from the outside
        yield return new MsalSilentTokenProvider(app, logger);

        if (WindowsIntegratedAuth.IsSupported())
        {
            yield return new MsalIntegratedWindowsAuthTokenProvider(app, logger);
        }

        // Use broker authentication first if enabled
        if (brokerEnabled)
        {
            yield return new MsalInteractiveTokenProvider(appInteractiveBroker, logger);
        }

        // Fallback to non-broker interactive browser auth
        yield return new MsalInteractiveTokenProvider(app, logger);

        // Fallback to device code flow as last resort
        yield return new MsalDeviceCodeTokenProvider(app, logger);
    }
}
