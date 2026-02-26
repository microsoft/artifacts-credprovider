// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Runtime.InteropServices;
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

    #region macOS CFRunLoop P/Invoke
    // MSAL's NativeInterop calls MSALRUNTIME_Runloop_run() (i.e. CFRunLoopRun) on the main
    // thread and only registers the CancellationToken callback AFTER that blocking call
    // returns.  On non-Intune-joined devices the macOS SSO Extension never responds, so
    // CFRunLoopRun blocks forever and the cancellation is never wired up.
    //
    // To break this deadlock we register our own cancellation callback BEFORE invoking MSAL,
    // calling CFRunLoopStop(CFRunLoopGetMain()) directly.  This unblocks MSALRUNTIME_Runloop_run
    // so MSAL's code can unwind and surface the cancellation/error.

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFRunLoopGetMain();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopStop(IntPtr runLoop);
    #endregion

    public bool CanGetToken(TokenRequest tokenRequest)
    {
        if (!tokenRequest.IsInteractive || !tokenRequest.CanShowDialog)
        {
            return false;
        }

        // Guard: this provider is only meaningful with a broker-enabled app.
        if (!app.AppConfig.IsBrokerEnabled)
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

        // On macOS, register a safety callback that stops the Core Foundation run loop if
        // cancellation fires.  The MSAL NativeInterop layer calls CFRunLoopRun() on the
        // main thread but only registers its own CancellationToken handler after the
        // blocking RunloopRun returns — creating a deadlock when the macOS SSO Extension
        // never responds (e.g. non-Intune-joined devices).  By stopping the run loop
        // ourselves, MSAL's code can unwind normally.
        CancellationTokenRegistration? cfRunLoopGuard = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                IntPtr mainRunLoop = CFRunLoopGetMain();
                cfRunLoopGuard = cts.Token.Register(() =>
                {
                    logger.LogDebug(Resources.MacBrokerCFRunLoopStop);
                    CFRunLoopStop(mainRunLoop);
                });
            }
            catch (Exception ex)
            {
                // If the P/Invoke fails (e.g. running on a non-macOS .NET build
                // that still reports OSPlatform.OSX), log and continue — the
                // worst case is the pre-existing hang behaviour.
                logger.LogTrace("CFRunLoopGetMain P/Invoke failed: {Message}", ex.Message);
            }
        }

        try
        {
            logger.LogInformation(Resources.MsalInteractivePrompt);

            AuthenticationResult? result = null;
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
        catch (MsalServiceException ex)
        {
            // After breaking the CFRunLoop on macOS, the broker may surface device-compliance
            // or registration errors.  Treat these as non-fatal so the system-browser fallback
            // provider can handle authentication.
            logger.LogWarning(Resources.MacBrokerServiceError, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            logger.LogWarning(ex.Message);
            return null;
        }
        finally
        {
            // Dispose the CFRunLoop cancellation guard so it doesn't fire after we leave.
            if (cfRunLoopGuard.HasValue)
            {
                cfRunLoopGuard.Value.Dispose();
            }
        }
    }
}
