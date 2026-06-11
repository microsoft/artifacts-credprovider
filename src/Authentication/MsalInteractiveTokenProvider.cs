// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Artifacts.Authentication;

/// <summary>
/// Interactive token provider that uses the system browser for authentication.
/// When broker is configured on the PCA, MSAL will try broker first and fall back to system browser.
/// On macOS, broker auth requires the main thread via <see cref="MacMainThreadScheduler"/>,
/// so this provider dispatches to the main thread when the scheduler is running.
/// </summary>
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
            logger.LogInformation(Resources.MsalInteractivePrompt);

            AuthenticationResult? result = null;

            // On macOS, broker auth requires the main thread. If the MacMainThreadScheduler
            // is running, dispatch to main thread so MSAL can try broker (and fall back to
            // system browser if broker is unavailable).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var scheduler = MacMainThreadScheduler.Instance();
                if (scheduler.IsRunning())
                {
                    await scheduler.RunOnMainThreadAsync(async () =>
                    {
                        result = await app.AcquireTokenInteractive(MsalConstants.AzureDevOpsScopes)
                            .WithPrompt(Prompt.SelectAccount)
                            .WithUseEmbeddedWebView(false)
                            .ExecuteAsync(cts.Token);
                    });

                    return result;
                }

                // Scheduler not running (e.g. dotnet tool shim where ManagedThreadId != 1).
                // Call directly — MSAL will skip broker and use system browser.
                logger.LogTrace(Resources.MacSchedulerNotRunningFallback);
            }

            result = await app.AcquireTokenInteractive(MsalConstants.AzureDevOpsScopes)
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
