// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Artifacts.Authentication;

/// <summary>
/// Interactive token provider that uses the MSAL broker (e.g. WAM on Windows, broker on macOS).
/// On macOS, broker auth requires the main thread via <see cref="MacMainThreadScheduler"/>.
/// If the scheduler is not running (e.g. ManagedThreadId != 1), this provider declines so the
/// non-broker <see cref="MsalInteractiveTokenProvider"/> can handle it via system browser instead.
/// </summary>
public class MsalBrokerInteractiveTokenProvider : ITokenProvider
{
    private readonly IPublicClientApplication app;
    private readonly ILogger logger;

    public MsalBrokerInteractiveTokenProvider(IPublicClientApplication app, ILogger logger)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "MSAL Broker Interactive";

    public bool IsInteractive => true;

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        if (!tokenRequest.IsInteractive || !tokenRequest.CanShowDialog)
        {
            return false;
        }

        // On macOS, broker auth requires the main thread scheduler to be running.
        // If it's not (e.g. ManagedThreadId != 1 in the dotnet tool shim), decline
        // so the non-broker MsalInteractiveTokenProvider handles it instead.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !MacMainThreadScheduler.Instance().IsRunning())
        {
            logger.LogTrace(Resources.MacSchedulerNotRunningBrokerSkipped);
            return false;
        }

        return true;
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
            var scheduler = MacMainThreadScheduler.Instance();

            if (scheduler.IsRunning())
            {
                await scheduler.RunOnMainThreadAsync(async () =>
                {
                    // Clear the SynchronizationContext so that if the macOS broker fails
                    // (e.g. FallbackToNativeMsal on non-Intune devices) and MSAL internally
                    // retries with system browser auth, the async continuations run on the
                    // thread pool instead of trying to post back to the main thread. Without
                    // this, the browser fallback's continuations deadlock waiting for the
                    // main thread which is blocked inside RunOnMainThreadAsync.
                    var savedContext = SynchronizationContext.Current;
                    SynchronizationContext.SetSynchronizationContext(null);
                    try
                    {
                        result = await app.AcquireTokenInteractive(MsalConstants.AzureDevOpsScopes)
                            .WithPrompt(Prompt.SelectAccount)
                            .WithUseEmbeddedWebView(false)
                            .ExecuteAsync(cts.Token);
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(savedContext);
                    }
                });
            }
            else
            {
                // Scheduler is not running but CanGetToken allowed us through (non-macOS).
                // Execute directly — WAM on Windows and broker on Linux don't need the macOS main thread.
                result = await app.AcquireTokenInteractive(MsalConstants.AzureDevOpsScopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync(cts.Token);
            }

            return result;
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            logger.LogWarning(ex.Message);
            return null;
        }
        catch (MsalException ex)
        {
            logger.LogWarning(Resources.MsalBrokerInteractiveFailed, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            logger.LogWarning(ex.Message);
            return null;
        }
    }
}
