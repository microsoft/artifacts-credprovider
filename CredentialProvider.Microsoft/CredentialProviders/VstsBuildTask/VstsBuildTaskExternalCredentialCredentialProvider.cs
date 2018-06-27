// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTask
{
    /*public interface EndpointCredentials
    {
        string Endpoint { get; }
        string Username { get; }
        string Password { get; }
    }*/

    public class EndpointCredentials
    {
        [JsonProperty("endpoint")]
        public string EndPoint { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("password")]
        public string Password { get; set; }
    }

    internal sealed class VstsBuildTaskExternalCredentialCredentialProvider : CredentialProviderBase
    {
        private string DefaultUsername = "VssSessionToken";
        private EndpointCredentials Credentials;

        public VstsBuildTaskExternalCredentialCredentialProvider(ILogger logger)
            : base(logger)
        {
        }

        protected override string LoggingName => nameof(VstsBuildTaskExternalCredentialCredentialProvider);

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            string feedEndPointsJson = Environment.GetEnvironmentVariable("VSS_NUGET_EXTERNAL_FEED_ENDPOINTS");
            if (string.IsNullOrWhiteSpace(feedEndPointsJson) == false)
            {
                string uriString = uri.ToString();
                Verbose($"URI: {uriString}");

                JObject feedEndPoints = JObject.Parse(feedEndPointsJson);
                JProperty endpointCredentials = feedEndPoints.Property("endpointCredentials");
                if (endpointCredentials == null)
                {
                    Verbose("No feed endpoints found");
                    Task.FromResult(false);
                }

                // Parse JSON from VSS_NUGET_EXTERNAL_FEED_ENDPOINTS
                bool matchingEndpointFound = FindMatchingEndpoint(endpointCredentials, uriString);
                if (matchingEndpointFound == true)
                {
                    return Task.FromResult(true);
                }

                Verbose("No provided endpoint matched the given uri");
                return Task.FromResult(false);
            }

            Verbose(Resources.BuildTaskEnvVarError);
            return Task.FromResult(false);
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            bool isRetry = request.IsRetry;

            if (isRetry)
            {
                return this.GetResponse(
                    Credentials.Username,
                    null,
                    string.Format(Resources.BuildTaskIsRetry, request.Uri.ToString()),
                    MessageResponseCode.Error);
            }

            return this.GetResponse(
                    Credentials.Username,
                    Credentials.Password,
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

        private bool FindMatchingEndpoint(JProperty endpointCredentials, string uri)
        {
            Verbose($"Looking for matching external endpoint credentials");
            foreach (JObject obj in endpointCredentials.Value)
            {
                EndpointCredentials credentials = obj.ToObject<EndpointCredentials>();
                if (credentials.EndPoint == null)
                {
                    Error($"Failed to parse endpoint.");
                    break;
                }

                if (credentials.EndPoint.Equals(uri, StringComparison.OrdinalIgnoreCase))
                {
                    Verbose($"Checking credentials for endpoint: {credentials.EndPoint}");

                    if (credentials.Password == null)
                    {
                        Error($"Failed to parse credentials.");
                        break;
                    }

                    Credentials = credentials;

                    if (credentials.Username == null)
                    {
                        Credentials.Username = DefaultUsername;
                    }

                    return true;
                }

                /*JProperty feedEndpoint = obj.Property("endpoint");
                if (feedEndpoint.Value.ToString().Equals(uri, StringComparison.OrdinalIgnoreCase))
                {
                    Verbose($"Checking credentials for endpoint: {feedEndpoint}");

                    JProperty password = obj.Property("password");
                    if (password == null)
                    {
                        Verbose("Failed to read credentials");
                        Task.FromResult(false);
                    }
                    Password = password.Value.ToString();

                    // Username can be null. Default: VssSessionToken
                    JProperty username = obj.Property("username");
                    if (username != null)
                    {
                        Username = username.Value.ToString();
                    }

                    return true;
                }*/
            }

            return false;
        }
    }
}
