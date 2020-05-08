// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

        public bool Interactive { get; } = false;
        public string Name { get; } = "ADAL Cache";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await adalTokenProvider.AcquireTokenSilentlyAsync(cancellationToken))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
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

        public bool Interactive { get; } = false;
        public string Name { get; } = "ADAL Windows Integrated Authentication";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            return (await adalTokenProvider.AcquireTokenWithWindowsIntegratedAuth(cancellationToken))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
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

        public bool Interactive { get; } = true;
        public string Name { get; } = "ADAL UI";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
        {
            logger.Minimal(string.Format(Resources.UIFlowStarted, this.Name));
            return (await adalTokenProvider.AcquireTokenWithUI(cancellationToken))?.AccessToken;
        }

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
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

        public bool Interactive { get; } = true;
        public string Name { get; } = "ADAL Device Code";

        public async Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken)
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

        public bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog)
        {
            return !isNonInteractive;
        }
    }

    /// <summary>
    /// A mechanism to obtain a bearer (e.g. AAD) token which can later be exchanged for an Azure DevOps session or personal access token.
    /// </summary>
    public interface IBearerTokenProvider
    {
        string Name { get; }

        bool Interactive { get; }

        bool ShouldRun(bool isRetry, bool isNonInteractive, bool canShowDialog);

        Task<string> GetTokenAsync(Uri uri, CancellationToken cancellationToken);
    }
}
