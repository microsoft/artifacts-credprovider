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
