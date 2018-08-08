// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Util;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpointCredentialProvider
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

internal sealed class VstsBuildTaskServiceEndpointCredentialProvider : CredentialProviderBase
    {
        private Dictionary<string, EndpointCredentials> Credentials = new Dictionary<string, EndpointCredentials>();

        public VstsBuildTaskServiceEndpointCredentialProvider(ILogger logger)
            : base(logger)
        {
        }

        public override bool IsCachable { get { return false; } }

        protected override string LoggingName => nameof(VstsBuildTaskServiceEndpointCredentialProvider);

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            try
            {
                string feedEndPointsJson = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);
                if (string.IsNullOrWhiteSpace(feedEndPointsJson) == false)
                {
                    string uriString = uri.ToString();
                    if (Credentials.ContainsKey(uriString) == false)
                    {
                        // Populate Credentials dictionary
                        ParseJsonToDictionary(feedEndPointsJson);
                    }

                    Verbose(Resources.EndpointMatchLookup);
                    if (Credentials.ContainsKey(uriString))
                    {
                        return Task.FromResult(true);
                    }

                    return Task.FromResult(false);
                }

                Verbose(Resources.BuildTaskEnvVarError);
                return Task.FromResult(false);
            }
            catch (Exception e)
            {
                Verbose(string.Format(Resources.VstsBuildTaskExternalCredentialCredentialProviderError, e));
                return Task.FromResult(false);
            }
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            Credentials.TryGetValue(request.Uri.ToString(), out EndpointCredentials matchingEndpoint);
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

        private void ParseJsonToDictionary(string feedEndPointsJson)
        {
            // Parse JSON from VSS_NUGET_EXTERNAL_FEED_ENDPOINTS
            Verbose(Resources.ParsingJson);
            EndpointCredentialsContainer endpointCredentials = JsonConvert.DeserializeObject<EndpointCredentialsContainer>(feedEndPointsJson);
            if (endpointCredentials == null)
            {
                Verbose(Resources.NoEndpointsFound);
                return;
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

                if (Credentials.ContainsKey(credentials.Endpoint) == false)
                {
                    Credentials.Add(credentials.Endpoint, credentials);
                }
            }
        }
    }
}
