// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    /// <summary>
    /// Acquire an AAD token via the Msal cache
    /// </summary>
    internal class MsalCacheBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IMsalTokenProvider MsalTokenProvider;

        public MsalCacheBearerTokenProvider(IMsalTokenProvider MsalTokenProvider)
        {
            this.MsalTokenProvider = MsalTokenProvider;
        }

        public bool Interactive { get; } = false;
        public string Name { get; } = "Msal Cache";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await MsalTokenProvider.AcquireTokenSilentlyAsync(cancellationToken))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return !isRetry;
        }
    }

    /// <summary>
    /// Acquire an AAD token via Windows Integrated Authentication
    /// </summary>
    internal class MsalWindowsIntegratedAuthBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IMsalTokenProvider MsalTokenProvider;

        public MsalWindowsIntegratedAuthBearerTokenProvider(IMsalTokenProvider MsalTokenProvider)
        {
            this.MsalTokenProvider = MsalTokenProvider;
        }

        public bool Interactive { get; } = false;
        public string Name { get; } = "Msal Windows Integrated Authentication";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await MsalTokenProvider.AcquireTokenWithWindowsIntegratedAuth(cancellationToken))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return WindowsIntegratedAuthUtils.SupportsWindowsIntegratedAuth() && EnvUtil.WindowsIntegratedAuthenticationEnabled();
        }
    }

    /// <summary>
    /// Acquire an AAD token by showing a sign-in UI
    /// </summary>
    internal class MsalUserInterfaceBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IMsalTokenProvider MsalTokenProvider;
        private ILogger logging;

        internal MsalUserInterfaceBearerTokenProvider(IMsalTokenProvider MsalTokenProvider, ILogger logging)
        {
            this.MsalTokenProvider = MsalTokenProvider;
            this.logging = logging;
        }

        public bool Interactive { get; } = true;
        public string Name { get; } = "Msal UI";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await MsalTokenProvider.AcquireTokenWithUI(cancellationToken, logging))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            // MSAL will use the system browser, this will work on ALL os's
            return !isNonInteractive && canShowDialog;
        }
    }

    /// <summary>
    /// Acquire an AAD token by presenting a "device code" and waiting for the user to sign in with it in a browser
    /// </summary>
    internal class MsalDeviceCodeFlowBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IMsalTokenProvider MsalTokenProvider;
        private readonly ILogger logger;

        public MsalDeviceCodeFlowBearerTokenProvider(
            IMsalTokenProvider MsalTokenProvider,
            ILogger logger)
        {
            this.MsalTokenProvider = MsalTokenProvider;
            this.logger = logger;
        }

        public bool Interactive { get; } = true;
        public string Name { get; } = "Msal Device Code";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await MsalTokenProvider.AcquireTokenWithDeviceFlowAsync(
                    (DeviceCodeResult deviceCodeResult) =>
                    {
                        logger.Minimal(string.Format(Resources.AdalDeviceFlowRequestedResource, uri.ToString()));
                        logger.Minimal(string.Format(Resources.AdalDeviceFlowMessage, deviceCodeResult.VerificationUrl, deviceCodeResult.UserCode));

                        return Task.CompletedTask;
                    },
                    cancellationToken,
                    logger))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return !isNonInteractive;
        }
    }
}
