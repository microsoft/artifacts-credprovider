// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public sealed class VstsCredentialProvider : CredentialProviderBase
    {
        private const string Username = "VssSessionToken";

        private readonly IAuthUtil authUtil;
        private readonly IBearerTokenProvidersFactory bearerTokenProvidersFactory;
        private readonly IAzureDevOpsSessionTokenFromBearerTokenProvider vstsSessionTokenProvider;

        public VstsCredentialProvider(
            ILogger logger,
            IAuthUtil authUtil,
            IBearerTokenProvidersFactory bearerTokenProvidersFactory,
            IAzureDevOpsSessionTokenFromBearerTokenProvider vstsSessionTokenProvider)
            : base(logger)
        {
            this.authUtil = authUtil;
            this.bearerTokenProvidersFactory = bearerTokenProvidersFactory;
            this.vstsSessionTokenProvider = vstsSessionTokenProvider;
        }

        protected override string LoggingName => nameof(VstsCredentialProvider);

        public override async Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            string buildTaskJsonEnvVar = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);
            string buildTaskPrefixesEnvVar = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskUriPrefixes);
            string buildTaskAccessTokenEnvVar = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskAccessToken);

            bool shouldUseBuildTaskCredProvider = string.IsNullOrWhiteSpace(buildTaskJsonEnvVar) == false 
                || string.IsNullOrWhiteSpace(buildTaskPrefixesEnvVar) == false 
                || string.IsNullOrWhiteSpace(buildTaskAccessTokenEnvVar) == false;
            if (shouldUseBuildTaskCredProvider)
            {
                // If env vars for build task cred providers are set, this cred provider cannot be used.
                Verbose(Resources.BuildTaskCredProviderIsUsedError);
                return await Task.FromResult(false);
            }

            var validHosts = EnvUtil.GetHostsFromEnvironment(Logger, EnvUtil.SupportedHostsEnvVar, new[]
            {
                ".pkgs.vsts.me", // DevFabric
                ".pkgs.codedev.ms", // DevFabric
                ".pkgs.codeapp.ms", // AppFabric
                ".pkgs.visualstudio.com", // Prod
                ".pkgs.dev.azure.com" // Prod
            });

            return validHosts.Any(host => uri.Host.EndsWith(host, StringComparison.OrdinalIgnoreCase)) || await authUtil.IsVstsUriAsync(uri);
        }

        public override async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            Uri authority = await authUtil.GetAadAuthorityUriAsync(request.Uri, cancellationToken);
            Verbose(string.Format(Resources.AdalUsingAuthority, authority));

            IEnumerable<IBearerTokenProvider> bearerTokenProviders = bearerTokenProvidersFactory.Get(authority.ToString());
            cancellationToken.ThrowIfCancellationRequested();

            // Try each bearer token provider (e.g. ADAL cache, ADAL WIA, ADAL UI, ADAL DeviceCode) in order.
            // Only consider it successful if the bearer token can be exchanged for an Azure DevOps token.
            foreach (IBearerTokenProvider bearerTokenProvider in bearerTokenProviders)
            {
                bool shouldRun = bearerTokenProvider.ShouldRun(request.IsRetry, request.IsNonInteractive, request.CanShowDialog);
                if (!shouldRun)
                {
                    Verbose(string.Format(Resources.NotRunningBearerTokenProvider, bearerTokenProvider.Name));
                    continue;
                }

                Verbose(string.Format(Resources.AttemptingToAcquireBearerTokenUsingProvider, bearerTokenProvider.Name));

                string bearerToken = null;
                try
                {
                    bearerToken = await bearerTokenProvider.GetTokenAsync(request.Uri, cancellationToken);
                }
                catch (Exception ex)
                {
                    Verbose(string.Format(Resources.BearerTokenProviderException, bearerTokenProvider.Name, ex));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bearerToken))
                {
                    Verbose(string.Format(Resources.BearerTokenProviderReturnedNull, bearerTokenProvider.Name));
                    continue;
                }

                Info(string.Format(Resources.AcquireBearerTokenSuccess, bearerTokenProvider.Name));
                Info(Resources.ExchangingBearerTokenForSessionToken);
                try
                {
                    string sessionToken = await vstsSessionTokenProvider.GetAzureDevOpsSessionTokenFromBearerToken(request, bearerToken, bearerTokenProvider.Interactive, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(sessionToken))
                    {
                        Verbose(string.Format(Resources.VSTSSessionTokenCreated, request.Uri.ToString()));
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
                    Verbose(string.Format(Resources.VSTSCreateSessionException, request.Uri, e.Message, e.StackTrace));
                }
            }

            Verbose(string.Format(Resources.VSTSCredentialsNotFound, request.Uri.ToString()));
            return null;
        }
    }
}