// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using Newtonsoft.Json;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Util;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint
{
    public class EndpointCredentials
    {
        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("password")]
        public string Password { get; set; }
        [JsonProperty("azureClientId")]
        public string ClientId { get; set; }
    }

    public class EndpointCredentialsContainer
    {
        [JsonProperty("endpointCredentials")]
        public EndpointCredentials[] EndpointCredentials { get; set; }
    }

    public sealed class VstsBuildTaskServiceEndpointCredentialProvider : CredentialProviderBase
    {
        private IAuthUtil AuthUtil;
        private ITokenProvidersFactory TokenProvidersFactory;
        private Lazy<Dictionary<string, EndpointCredentials>> LazyCredentials;

        // Dictionary that maps an endpoint string to EndpointCredentials
        private Dictionary<string, EndpointCredentials> Credentials => LazyCredentials.Value;
            
        public VstsBuildTaskServiceEndpointCredentialProvider(
            IAuthUtil authutil,
            ITokenProvidersFactory tokenProvidersFactory,
            IAzureDevOpsSessionTokenFromBearerTokenProvider VstsSessionTokenProvider,
            ILogger logger)
            : base(logger, VstsSessionTokenProvider)
        {
            AuthUtil = authutil;
            TokenProvidersFactory = tokenProvidersFactory;
            LazyCredentials = new Lazy<Dictionary<string, EndpointCredentials>>(() =>
            {
                return FeedEndpointCredentialsUtil.ParseJsonToDictionary(logger);
            });
        }

        public override bool IsCachable { get { return false; } }

        protected override string LoggingName => nameof(VstsBuildTaskServiceEndpointCredentialProvider);

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            string feedEndPointsJson = EnvUtil.GetFeedEndpointCredentials();
            if (string.IsNullOrWhiteSpace(feedEndPointsJson))
            {
                Verbose(Resources.BuildTaskEndpointEnvVarError);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public override async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Verbose(string.Format(Resources.IsRetry, request.IsRetry));

            string uriString = request.Uri.AbsoluteUri;
            bool endpointFound = Credentials.TryGetValue(uriString, out EndpointCredentials matchingEndpoint);
            if (!endpointFound)
            {
                Verbose(string.Format(Resources.BuildTaskEndpointNoMatchingUrl, uriString));
                return await GetResponse(
                    null,
                    null,
                    string.Format(Resources.BuildTaskFailedToAuthenticate, uriString),
                    MessageResponseCode.Error);
            }

            if (!string.IsNullOrWhiteSpace(matchingEndpoint.Password))
            {
                Verbose(string.Format(Resources.BuildTaskEndpointMatchingUrlFound, uriString));
                return await GetResponse(
                    matchingEndpoint.Username,
                    matchingEndpoint.Password,
                    null,
                    MessageResponseCode.Success);
            }

            if (!string.IsNullOrWhiteSpace(matchingEndpoint.ClientId))
            {
                Uri authority = await AuthUtil.GetAadAuthorityUriAsync(request.Uri, cancellationToken);
                Verbose(string.Format(Resources.UsingAuthority, authority));

                IEnumerable<ITokenProvider> tokenProviders = await TokenProvidersFactory.GetAsync(authority);
                cancellationToken.ThrowIfCancellationRequested();

                var tokenRequest = new TokenRequest(request.Uri)
                {
                    IsRetry = request.IsRetry,
                    IsNonInteractive = true,
                    CanShowDialog = false,
                    IsWindowsIntegratedAuthEnabled = false,
                    LoginHint = EnvUtil.GetMsalLoginHint(),
                    InteractiveTimeout = TimeSpan.FromSeconds(EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(Logger)),
                    ClientId = matchingEndpoint.ClientId,
                };

                return await GetVstsTokenAsync(request, tokenProviders, tokenRequest, cancellationToken);
            }

            Verbose(string.Format(Resources.BuildTaskEndpointNoMatchingUrl, uriString));
            return await GetResponse(
                null,
                null,
                string.Format(Resources.BuildTaskFailedToAuthenticate, uriString),
                MessageResponseCode.Error);
        }

        private Task<GetAuthenticationCredentialsResponse> GetResponse(string username, string password, string message, MessageResponseCode responseCode)
        {
            return Task.FromResult(new GetAuthenticationCredentialsResponse(
                    username: username,
                    password: password,
                    message: message,
                    authenticationTypes:
                    [
                        "Basic"
                    ],
                    responseCode: responseCode));
        }
    }
}
