// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private Lazy<Dictionary<string, EndpointCredentials>> LazyCredentials;
        private ITokenProvidersFactory TokenProvidersFactory;
        private IAuthUtil AuthUtil;

        // Dictionary that maps an endpoint string to EndpointCredentials
        private Dictionary<string, EndpointCredentials> Credentials => LazyCredentials.Value;
            
        public VstsBuildTaskServiceEndpointCredentialProvider(ILogger logger, ITokenProvidersFactory tokenProvidersFactory, IAuthUtil authUtil)
            : base(logger)
        {
            TokenProvidersFactory = tokenProvidersFactory;
            LazyCredentials = new Lazy<Dictionary<string, EndpointCredentials>>(() =>
            {
                return FeedEndpointCredentialsUtil.ParseJsonToDictionary(logger);
            });
            AuthUtil = authUtil;
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
            if (endpointFound && !string.IsNullOrWhiteSpace(matchingEndpoint.Password))
            {
                Verbose(string.Format(Resources.BuildTaskEndpointMatchingUrlFound, uriString));
                return GetResponse(
                    matchingEndpoint.Username,
                    matchingEndpoint.Password,
                    null,
                    MessageResponseCode.Success);
            }

            if (endpointFound && !string.IsNullOrWhiteSpace(matchingEndpoint.ClientId))
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

                foreach(var tokenProvider in tokenProviders.Where(x => x.Name == "MSAL Managed Identity").ToList())
                {
                    bool shouldRun = tokenProvider.CanGetToken(tokenRequest);
                    if (!shouldRun)
                    {
                        Verbose(string.Format(Resources.NotRunningBearerTokenProvider, tokenProvider.Name));
                        continue;
                    }

                    Verbose(string.Format(Resources.AttemptingToAcquireBearerTokenUsingProvider, tokenProvider.Name));

                    string bearerToken;
                    try
                    {
                        var result = await tokenProvider.GetTokenAsync(tokenRequest, cancellationToken);
                        bearerToken = result?.AccessToken;
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
                    return GetResponse(
                        matchingEndpoint.Username,
                        bearerToken,
                        null,
                        MessageResponseCode.Success);
                } 
            }

            Verbose(string.Format(Resources.BuildTaskEndpointNoMatchingUrl, uriString));
            return GetResponse(
                null,
                null,
                string.Format(Resources.BuildTaskFailedToAuthenticate, uriString),
                MessageResponseCode.Error);
        }

        private GetAuthenticationCredentialsResponse GetResponse(string username, string password, string message, MessageResponseCode responseCode)
        {
            return new GetAuthenticationCredentialsResponse(
                    username: username,
                    password: password,
                    message: message,
                    authenticationTypes: new List<string>
                    {
                        "Basic"
                    },
                    responseCode: responseCode);
        }
    }
}
