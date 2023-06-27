// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalInteractiveTokenProvider : ITokenProvider
{
    private readonly IPublicClientApplication app;
    private readonly ILogger logger;

    public MsalInteractiveTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "MSAL Interactive";

    public bool IsInteractive => true;

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        // MSAL will use the system browser, this will work on all OS's
        return tokenRequest.IsInteractive && tokenRequest.CanShowDialog;
    }

    public async Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken = default)
    {
        if (!app.IsUserInteractive())
        {
            logger.LogTrace(Resources.MsalNotUserInteractive);
            return null;
        }

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(tokenRequest.InteractiveTimeout);

        try
        {
            var result = await app.AcquireTokenInteractive(MsalConstants.AzureDevOpsScopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync(cts.Token);

            return result;
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            logger.LogWarning(ex.Message);
            return null;
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            logger.LogWarning(ex.Message);
            return null;
        }
    }
}
