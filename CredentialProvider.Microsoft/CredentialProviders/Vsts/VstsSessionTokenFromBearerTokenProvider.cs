// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class VstsSessionTokenFromBearerTokenProvider : IAzureDevOpsSessionTokenFromBearerTokenProvider
    {
        private const double DefaultSessionTimeHours = 4;
        private const double DefaultPersonalAccessTimeHours = 2160; // 90 days
        private readonly IAuthUtil authUtil;
        private readonly ILogger logger;

        public VstsSessionTokenFromBearerTokenProvider(IAuthUtil authUtil, ILogger logger)
        {
            this.authUtil = authUtil;
            this.logger = logger;
        }

        public async Task<string> GetAzureDevOpsSessionTokenFromBearerToken(
            GetAuthenticationCredentialsRequest request,
            string bearerToken,
            bool bearerTokenObtainedInteractively,
            CancellationToken cancellationToken)
        {
            // Allow the user to choose their token type
            // If they don't and interactive auth was required, then prefer a PAT so we can safely default to a much longer validity period
            VstsTokenType tokenType = EnvUtil.GetVstsTokenType() ??
                (bearerTokenObtainedInteractively
                    ? VstsTokenType.Compact
                    : VstsTokenType.SelfDescribing);

            // Allow the user to override the validity period
            TimeSpan? preferredTokenTime = EnvUtil.GetSessionTimeFromEnvironment(logger);
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
            logger.Verbose(string.Format(Resources.VSTSSessionTokenValidity, tokenType.ToString(), sessionTimeSpan.ToString(), endTime.ToUniversalTime().ToString()));
            VstsSessionTokenClient sessionTokenClient = new VstsSessionTokenClient(request.Uri, bearerToken, authUtil);
            return await sessionTokenClient.CreateSessionTokenAsync(tokenType, endTime, cancellationToken);
        }
    }
}
