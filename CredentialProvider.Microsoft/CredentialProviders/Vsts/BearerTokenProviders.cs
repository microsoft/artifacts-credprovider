// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    /// <summary>
    /// Acquire an AAD token via the ADAL cache
    /// </summary>
    public class AdalCacheBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IAdalTokenProvider adalTokenProvider;

        public AdalCacheBearerTokenProvider(IAdalTokenProvider adalTokenProvider)
        {
            this.adalTokenProvider = adalTokenProvider;
        }

        public override bool Interactive { get; } = false;
        public override string Name { get; } = "ADAL Cache";

        public override async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await adalTokenProvider.AcquireTokenSilentlyAsync(cancellationToken))?.AccessToken;
        }

        public override bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return !isRetry;
        }
    }

    /// <summary>
    /// Acquire an AAD token via Windows Integrated Authentication
    /// </summary>
    public class WindowsIntegratedAuthBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IAdalTokenProvider adalTokenProvider;

        public WindowsIntegratedAuthBearerTokenProvider(IAdalTokenProvider adalTokenProvider)
        {
            this.adalTokenProvider = adalTokenProvider;
        }

        public override bool Interactive { get; } = false;
        public override string Name { get; } = "ADAL Windows Integrated Authentication";

        public override async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await adalTokenProvider.AcquireTokenWithWindowsIntegratedAuth(cancellationToken))?.AccessToken;
        }

        public override bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return WindowsIntegratedAuthUtils.SupportsWindowsIntegratedAuth() && EnvUtil.WindowsIntegratedAuthenticationEnabled();
        }
    }

    /// <summary>
    /// Acquire an AAD token by showing a sign-in UI
    /// </summary>
    public class UserInterfaceBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IAdalTokenProvider adalTokenProvider;
        private readonly ILogger logger;

        public UserInterfaceBearerTokenProvider(IAdalTokenProvider adalTokenProvider, ILogger logger)
        {
            this.adalTokenProvider = adalTokenProvider;
            this.logger = logger;
        }

        public override bool Interactive { get; } = true;
        public override string Name { get; } = "ADAL UI";

        public override async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            logger.Minimal(string.Format(Resources.UIFlowStarted, this.Name, uri.AbsoluteUri));
            return (await adalTokenProvider.AcquireTokenWithUI(cancellationToken))?.AccessToken;
        }

        public override bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
#if NETFRAMEWORK
            return !isNonInteractive && canShowDialog && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
            // no ADAL UI in netcore
            return false;
#endif
        }
    }

    /// <summary>
    /// Acquire an AAD token by presenting a "device code" and waiting for the user to sign in with it in a browser
    /// </summary>
    public class DeviceCodeFlowBearerTokenProvider : IBearerTokenProvider
    {
        private readonly IAdalTokenProvider adalTokenProvider;
        private readonly ILogger logger;

        public DeviceCodeFlowBearerTokenProvider(
            IAdalTokenProvider adalTokenProvider,
            ILogger logger)
        {
            this.adalTokenProvider = adalTokenProvider;
            this.logger = logger;
        }

        public override bool Interactive { get; } = true;
        public override string Name { get; } = "ADAL Device Code";

        public override async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await adalTokenProvider.AcquireTokenWithDeviceFlowAsync(
                    (DeviceCodeResult deviceCodeResult) =>
                    {
                        logger.Minimal(string.Format(Resources.AdalDeviceFlowRequestedResource, uri.ToString()));
                        logger.Minimal(string.Format(Resources.AdalDeviceFlowMessage, deviceCodeResult.VerificationUrl, deviceCodeResult.UserCode));

                        return Task.CompletedTask;
                    },
                    cancellationToken,
                    logger))?.AccessToken;
        }

        public override bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return !isNonInteractive;
        }
    }

    /// <summary>
    /// A mechanism to obtain a bearer (e.g. AAD) token which can later be exchanged for an Azure DevOps session or personal access token.
    /// </summary>
    public abstract class IBearerTokenProvider : ITokenProvider
    {
        public abstract string Name { get; }

        public abstract bool Interactive { get; }

        public bool IsInteractive => Interactive;

        public abstract bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog);

        public abstract Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken);

        public bool CanGetToken(TokenRequest tokenRequest)
        {
            return ShouldRun(tokenRequest.IsRetry, tokenRequest.IsNonInteractive, tokenRequest.CanShowDialog);
        }

        public async Task<Microsoft.Identity.Client.AuthenticationResult> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken = default)
        {
            var token = await GetTokenAsync(tokenRequest.Uri, cancellationToken);
            return new Microsoft.Identity.Client.AuthenticationResult(token, false, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, null, null, null, Guid.Empty);
        }
    }
}
