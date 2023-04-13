// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    /// <summary>
    /// Acquire an AAD token via the Msal cache
    /// </summary>
    internal class MsalSilentBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IMsalTokenProvider msalTokenProvider;

        public MsalSilentBearerTokenProvider(IMsalTokenProvider msalTokenProvider)
        {
            this.msalTokenProvider = msalTokenProvider;
        }

        public bool Interactive { get; } = false;
        public string Name => $"MSAL Silent " + msalTokenProvider.NameSuffix;

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await msalTokenProvider.AcquireTokenSilentlyAsync(cancellationToken))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            // Always run this and rely on MSAL to return a valid token. Previously, AcquireTokenByIntegratedWindowsAuth
            // would return cached tokens based on the user principal name, so this token provider could be skipped. Now,
            // cached and broker accounts and any cached tokens are returned via AcquireTokenSilent. MSAL will ensure any
            // returned token is refreshed as needed to be valid upon return.
            return true;
        }
    }

    /// <summary>
    /// Acquire an AAD token via Windows Integrated Authentication
    /// </summary>
    internal class MsalWindowsIntegratedAuthBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IMsalTokenProvider msalTokenProvider;

        public MsalWindowsIntegratedAuthBearerTokenProvider(IMsalTokenProvider msalTokenProvider)
        {
            this.msalTokenProvider = msalTokenProvider;
        }

        public bool Interactive { get; } = false;
        public string Name { get; } = "MSAL Windows Integrated Authentication";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await msalTokenProvider.AcquireTokenWithWindowsIntegratedAuth(cancellationToken))?.AccessToken;
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
        private readonly IMsalTokenProvider msalTokenProvider;

        internal MsalUserInterfaceBearerTokenProvider(IMsalTokenProvider msalTokenProvider)
        {
            this.msalTokenProvider = msalTokenProvider;
        }

        public bool Interactive { get; } = true;
        public string Name { get; } = "Msal UI";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await msalTokenProvider.AcquireTokenWithUI(cancellationToken, this.msalTokenProvider.Logger))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            // MSAL will use the system browser, this will work on all OS's
            return !isNonInteractive && canShowDialog;
        }
    }

    /// <summary>
    /// Acquire an AAD token by presenting a "device code" and waiting for the user to sign in with it in a browser
    /// </summary>
    internal class MsalDeviceCodeFlowBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IMsalTokenProvider msalTokenProvider;

        public MsalDeviceCodeFlowBearerTokenProvider(IMsalTokenProvider msalTokenProvider)
        {
            this.msalTokenProvider = msalTokenProvider;
        }

        public bool Interactive { get; } = true;
        public string Name { get; } = "Msal Device Code";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await msalTokenProvider.AcquireTokenWithDeviceFlowAsync(
                    (DeviceCodeResult deviceCodeResult) =>
                    {
                        this.msalTokenProvider.Logger.Minimal(string.Format(Resources.AdalDeviceFlowRequestedResource, uri.ToString()));
                        this.msalTokenProvider.Logger.Minimal(string.Format(Resources.AdalDeviceFlowMessage, deviceCodeResult.VerificationUrl, deviceCodeResult.UserCode));

                        return Task.CompletedTask;
                    },
                    cancellationToken,
                    this.msalTokenProvider.Logger))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return !isNonInteractive;
        }
    }
}
