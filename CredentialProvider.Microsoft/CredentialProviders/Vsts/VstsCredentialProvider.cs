// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using Microsoft.Identity.Client;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public sealed class VstsCredentialProvider : CredentialProviderBase
    {
        private const string Username = "VssSessionToken";

        private readonly IAuthUtil authUtil;
        private readonly ITokenProvidersFactory tokenProvidersFactory;
        private readonly IAzureDevOpsSessionTokenFromBearerTokenProvider vstsSessionTokenProvider;

        public VstsCredentialProvider(
            ILogger logger,
            IAuthUtil authUtil,
            ITokenProvidersFactory tokenProvidersFactory,
            IAzureDevOpsSessionTokenFromBearerTokenProvider vstsSessionTokenProvider)
            : base(logger)
        {
            this.authUtil = authUtil;
            this.tokenProvidersFactory = tokenProvidersFactory;
            this.vstsSessionTokenProvider = vstsSessionTokenProvider;
        }

        protected override string LoggingName => nameof(VstsCredentialProvider);

        public override async Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            // If for any reason we reach this point and any of the three build task env vars are set,
            // we should not try get credentials with this cred provider.
            string feedEndPointsJsonEnvVar = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);
            string uriPrefixesStringEnvVar = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskUriPrefixes);
            string accessTokenEnvVar = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskAccessToken);

            if (string.IsNullOrWhiteSpace(feedEndPointsJsonEnvVar) == false || string.IsNullOrWhiteSpace(uriPrefixesStringEnvVar) == false || string.IsNullOrWhiteSpace(accessTokenEnvVar) == false)
            {
                Verbose(Resources.BuildTaskCredProviderIsUsedError);
                return false;
            }

            var validHosts = EnvUtil.GetHostsFromEnvironment(Logger, EnvUtil.SupportedHostsEnvVar, new[]
            {
                ".pkgs.vsts.me", // DevFabric
                "pkgs.codedev.ms", // DevFabric
                "pkgs.codeapp.ms", // AppFabric
                ".pkgs.visualstudio.com", // Prod
                "pkgs.dev.azure.com" // Prod
            });

            bool isValidHost = validHosts.Any(host => host.StartsWith(".") ?
                uri.Host.EndsWith(host, StringComparison.OrdinalIgnoreCase) :
                uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
            if (isValidHost)
            {
                Verbose(string.Format(Resources.HostAccepted, uri.Host));
                return true;
            }

            var azDevOpsType = await authUtil.GetAzDevDeploymentType(uri);
            if (azDevOpsType == AzDevDeploymentType.Hosted)
            {
                Verbose(Resources.ValidHeaders);
                return true;
            }

            if (azDevOpsType == AzDevDeploymentType.OnPrem)
            {
                Verbose(Resources.OnPremDetected);
                return false;
            }

            Verbose(string.Format(Resources.ExternalUri, uri));
            return false;
        }

        public override async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            var forceCanShowDialogTo = EnvUtil.ForceCanShowDialogTo();
            var canShowDialog = request.CanShowDialog;
            if (forceCanShowDialogTo.HasValue)
            {
                Logger.Verbose(string.Format(Resources.ForcingCanShowDialogFromTo, request.CanShowDialog, forceCanShowDialogTo.Value));
                canShowDialog = forceCanShowDialogTo.Value;
            }

            Uri authority = await authUtil.GetAadAuthorityUriAsync(request.Uri, cancellationToken);
            Verbose(string.Format(Resources.UsingAuthority, authority));

            IEnumerable<ITokenProvider> tokenProviders = await tokenProvidersFactory.GetAsync(authority);
            cancellationToken.ThrowIfCancellationRequested();

            var tokenRequest = new TokenRequest(request.Uri)
            {
                IsRetry = request.IsRetry,
                IsNonInteractive = request.IsNonInteractive,
                CanShowDialog = canShowDialog,
                IsWindowsIntegratedAuthEnabled = EnvUtil.WindowsIntegratedAuthenticationEnabled(),
                LoginHint = EnvUtil.GetMsalLoginHint(),
                InteractiveTimeout = TimeSpan.FromSeconds(EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(Logger)),
                DeviceCodeResultCallback = (DeviceCodeResult deviceCodeResult) =>
                {
                    Logger.Minimal(string.Format(Resources.DeviceFlowRequestedResource, request.Uri.ToString()));
                    Logger.Minimal(string.Format(Resources.DeviceFlowMessage, deviceCodeResult.VerificationUrl, deviceCodeResult.UserCode));

                    return Task.CompletedTask;
                }
            };

            // Try each bearer token provider (e.g. cache, WIA, UI, DeviceCode) in order.
            // Only consider it successful if the bearer token can be exchanged for an Azure DevOps token.
            foreach (ITokenProvider tokenProvider in tokenProviders)
            {
                bool shouldRun = tokenProvider.CanGetToken(tokenRequest);
                if (!shouldRun)
                {
                    Verbose(string.Format(Resources.NotRunningBearerTokenProvider, tokenProvider.Name));
                    continue;
                }

                Verbose(string.Format(Resources.AttemptingToAcquireBearerTokenUsingProvider, tokenProvider.Name));

                string bearerToken = null;
                try
                {
                    var result = await tokenProvider.GetTokenAsync(tokenRequest, cancellationToken);
                    bearerToken = result.AccessToken;
                }
                catch (Exception ex)
                {
                    Verbose(string.Format(Resources.BearerTokenProviderException, tokenProvider.Name, ex));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bearerToken))
                {
                    Verbose(string.Format(Resources.BearerTokenProviderReturnedNull, tokenProvider.Name));
                    continue;
                }

                Info(string.Format(Resources.AcquireBearerTokenSuccess, tokenProvider.Name));
                Info(Resources.ExchangingBearerTokenForSessionToken);
                try
                {
                    string sessionToken = await vstsSessionTokenProvider.GetAzureDevOpsSessionTokenFromBearerToken(request, bearerToken, tokenProvider.IsInteractive, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(sessionToken))
                    {
                        Verbose(string.Format(Resources.VSTSSessionTokenCreated, request.Uri.AbsoluteUri));
                        return new GetAuthenticationCredentialsResponse(
                            Username,
                            sessionToken,
                            message: null,
                            authenticationTypes: new List<string>() { "Basic" },
                            responseCode: MessageResponseCode.Success);
                    }
                }
                catch (Exception e)
                {
                    Verbose(string.Format(Resources.VSTSCreateSessionException, request.Uri.AbsoluteUri, e.Message, e.StackTrace));
                }
            }

            Verbose(string.Format(Resources.VSTSCredentialsNotFound, request.Uri.AbsoluteUri));
            return null;
        }
    }
}
