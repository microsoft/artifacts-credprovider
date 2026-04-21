// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Artifacts.Authentication;

/// <summary>
/// Interactive token provider that uses the MSAL broker (e.g. WAM on Windows, broker on macOS).
/// On macOS, the broker requires the main thread's message loop (via <see cref="MacMainThreadScheduler"/>)
/// to be running, and an SSO extension (e.g. Company Portal) to be installed.
///
/// If no SSO extension is detected, this provider declines to avoid a deadlock in the native
/// MSAL broker runtime that occurs when FallbackToNativeMsal is triggered on non-Intune devices.
///
/// If the scheduler is not running (e.g. ManagedThreadId != 1), this provider declines so the
/// non-broker <see cref="MsalInteractiveTokenProvider"/> can handle it via system browser instead.
/// </summary>
public class MsalBrokerInteractiveTokenProvider : ITokenProvider
{
    private static readonly Lazy<bool> s_isMacSsoExtensionAvailable = new Lazy<bool>(DetectMacSsoExtension);

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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (!MacMainThreadScheduler.Instance().IsRunning())
            {
                logger.LogTrace(Resources.MacSchedulerNotRunningBrokerSkipped);
                return false;
            }

            // On macOS, skip broker auth if no Microsoft SSO extension is installed.
            // Without an SSO extension, the MSAL native broker runtime deadlocks
            // inside a synchronous GCD dispatch when FallbackToNativeMsal is triggered.
            if (!s_isMacSsoExtensionAvailable.Value)
            {
                logger.LogTrace("No Microsoft SSO extension detected; skipping broker interactive auth to avoid native deadlock.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether a Microsoft SSO extension is available on macOS by querying pluginkit.
    /// Result is cached for the lifetime of the process via <see cref="s_isMacSsoExtensionAvailable"/>.
    /// Workaround for https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/5940
    /// </summary>
    private static bool DetectMacSsoExtension()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "/usr/bin/pluginkit";
            process.StartInfo.Arguments = "-m -p com.apple.AppSSO.idp-extension";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return output.Contains("com.microsoft.", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If we can't determine SSO extension state, allow broker to try.
            return true;
        }
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
                    result = await app.AcquireTokenInteractive(MsalConstants.AzureDevOpsScopes)
                        .WithPrompt(Prompt.SelectAccount)
                        .WithUseEmbeddedWebView(false)
                        .ExecuteAsync(cts.Token);
                });
            }
            else
            {
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
