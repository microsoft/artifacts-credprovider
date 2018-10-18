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
        private const string TokenScope = "vso.packaging_write vso.drop_write";
        private const double DefaultSessionTimeHours = 4;
        private const double DefaultPersonalAccessTimeHours = 2160; // 90 days

        private readonly IAuthUtil authUtil;
        private readonly IBearerTokenProvider bearerTokenProvider;

        public VstsCredentialProvider(ILogger logger)
            : base(logger)
        {
            this.authUtil = new AuthUtil(logger);
            this.bearerTokenProvider = new BearerTokenProvider(logger);
        }

        public VstsCredentialProvider(ILogger logger, IAuthUtil authUtil, IBearerTokenProvider bearerTokenProvider)
            : base(logger)
        {
            this.authUtil = authUtil;
            this.bearerTokenProvider = bearerTokenProvider;
        }

        protected override string LoggingName => nameof(VstsCredentialProvider);

        public override async Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
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
            // If this is a retry, let's try without sending a retry request to the underlying bearerTokenProvider
            if (request.IsRetry)
            {
                var responseWithNoRetry = await HandleRequestAsync(GetNonRetryRequest(request), cancellationToken);
                if (responseWithNoRetry?.ResponseCode == MessageResponseCode.Success)
                {
                    return responseWithNoRetry;
                }
            }

            try
            {
                BearerTokenResult bearerTokenResult = await bearerTokenProvider.GetAsync(request.Uri, isRetry: false, request.IsNonInteractive, request.CanShowDialog, cancellationToken);
                if (bearerTokenResult == null || string.IsNullOrWhiteSpace(bearerTokenResult.Token))
                {
                    return new GetAuthenticationCredentialsResponse(
                        username: null,
                        password: null,
                        message: Resources.BearerTokenFailed,
                        authenticationTypes: null,
                        responseCode: MessageResponseCode.Error);
                }

                // Allow the user to choose their token type
                // If they don't and interactive auth was required, then prefer a PAT so we can safely default to a much longer validity period
                VstsTokenType tokenType = EnvUtil.GetVstsTokenType() ??
                    (bearerTokenResult.ObtainedInteractively
                        ? VstsTokenType.Compact
                        : VstsTokenType.SelfDescribing);

                // Allow the user to override the validity period
                TimeSpan? preferredTokenTime = EnvUtil.GetSessionTimeFromEnvironment(Logger);
                TimeSpan sessionTimeSpan;
                if (tokenType == VstsTokenType.Compact)
                {
                    // Allow Personal Access Tokens to be as long as SPS will grant, since they're easily revokable
                    sessionTimeSpan = preferredTokenTime ?? TimeSpan.FromHours(DefaultPersonalAccessTimeHours);
                }
                else
                {
                    // But limit self-describing session tokens to a strict 24 hours, since they're harder to revoke
                    sessionTimeSpan = preferredTokenTime ?? TimeSpan.FromHours(DefaultSessionTimeHours);
                    if (sessionTimeSpan >= TimeSpan.FromHours(24))
                    {
                        sessionTimeSpan = TimeSpan.FromHours(24);
                    }
                }

                DateTime endTime = DateTime.UtcNow + sessionTimeSpan;
                Verbose(string.Format(Resources.VSTSSessionTokenValidity, tokenType.ToString(), sessionTimeSpan.ToString(), endTime.ToUniversalTime().ToString()));
                var sessionTokenClient = new VstsSessionTokenClient(request.Uri, bearerTokenResult.Token, authUtil);
                var sessionToken = await sessionTokenClient.CreateSessionTokenAsync(tokenType, endTime, cancellationToken);

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

            Verbose(string.Format(Resources.VSTSCredentialsNotFound, request.Uri.ToString()));
            return null;
        }

        private GetAuthenticationCredentialsRequest GetNonRetryRequest(GetAuthenticationCredentialsRequest request)
        {
            return new GetAuthenticationCredentialsRequest(request.Uri, isRetry: false, request.IsNonInteractive, request.CanShowDialog);
        }
    }
}