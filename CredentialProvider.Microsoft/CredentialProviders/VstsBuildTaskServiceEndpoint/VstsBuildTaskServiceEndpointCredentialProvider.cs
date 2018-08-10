// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol.Plugins;
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
    }

    public class EndpointCredentialsContainer
    {
        [JsonProperty("endpointCredentials")]
        public EndpointCredentials[] EndpointCredentials { get; set; }
    }

    public sealed class VstsBuildTaskServiceEndpointCredentialProvider : CredentialProviderBase
    {
        private Lazy<Dictionary<string, EndpointCredentials>> LazyCredentials;
        private Dictionary<string, EndpointCredentials> Credentials => LazyCredentials.Value;

        public VstsBuildTaskServiceEndpointCredentialProvider(ILogger logger)
            : base(logger)
        {
            LazyCredentials = new Lazy<Dictionary<string, EndpointCredentials>>(() =>
            {
                return ParseJsonToDictionary();
            });
        }

        public override bool IsCachable { get { return false; } }

        protected override string LoggingName => nameof(VstsBuildTaskServiceEndpointCredentialProvider);

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            string feedEndPointsJson = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);
            if (string.IsNullOrWhiteSpace(feedEndPointsJson))
            {
                Verbose(Resources.BuildTaskEndpointEnvVarError);
                return Task.FromResult(false);
            }

            string uriString = uri.ToString();
            if (Credentials.ContainsKey(uriString))
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            bool endpointFound = Credentials.TryGetValue(request.Uri.ToString(), out EndpointCredentials matchingEndpoint);
            if (endpointFound)
            {
                string username = matchingEndpoint.Username;

                // Should fail on retry because the token is provided through an env var. Retry is not going to help.
                if (request.IsRetry)
                {
                    return GetResponse(
                        username,
                        null,
                        string.Format(Resources.BuildTaskIsRetry, request.Uri.ToString()),
                        MessageResponseCode.Error);
                }

                return GetResponse(
                    username,
                    matchingEndpoint.Password,
                    null,
                    MessageResponseCode.Success);
            }

            return GetResponse(
                null,
                null,
                string.Format(Resources.BuildTaskIsRetry, request.Uri.ToString()),
                MessageResponseCode.Error);
        }

        private Task<GetAuthenticationCredentialsResponse> GetResponse(string username, string password, string message, MessageResponseCode responseCode)
        {
            return Task.FromResult(new GetAuthenticationCredentialsResponse(
                    username: username,
                    password: password,
                    message: message,
                    authenticationTypes: new List<string>
                    {
                        "Basic"
                    },
                    responseCode: responseCode));
        }

        private Dictionary<string, EndpointCredentials> ParseJsonToDictionary()
        {
            string feedEndPointsJson = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);

            try
            {
                // Parse JSON from VSS_NUGET_EXTERNAL_FEED_ENDPOINTS
                Verbose(Resources.ParsingJson);
                Dictionary<string, EndpointCredentials> credsResult = new Dictionary<string, EndpointCredentials>();
                EndpointCredentialsContainer endpointCredentials = JsonConvert.DeserializeObject<EndpointCredentialsContainer>(feedEndPointsJson);
                if (endpointCredentials == null)
                {
                    Verbose(Resources.NoEndpointsFound);
                    return credsResult;
                }

                foreach (EndpointCredentials credentials in endpointCredentials.EndpointCredentials)
                {
                    if (credentials == null)
                    {
                        Verbose(Resources.EndpointParseFailure);
                        break;
                    }

                    if (credentials.Username == null)
                    {
                        credentials.Username = "VssSessionToken";
                    }

                    if (credsResult.ContainsKey(credentials.Endpoint) == false)
                    {
                        credsResult.Add(credentials.Endpoint, credentials);
                    }
                }

                return credsResult;
            }
            catch (Exception e)
            {
                Verbose(string.Format(Resources.VstsBuildTaskExternalCredentialCredentialProviderError, e));
                throw;
            }
        }
    }
}
