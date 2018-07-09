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

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTaskExternalCredentialCredential
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

    internal sealed class VstsBuildTaskExternalCredentialCredentialProvider : CredentialProviderBase
    {
        public override string Username { get; set; }
        public EndpointCredentials Credentials;

        public VstsBuildTaskExternalCredentialCredentialProvider(ILogger logger)
            : base(logger)
        {
            this.Username = "VssSessionToken";
        }

        public override string LoggingName => nameof(VstsBuildTaskExternalCredentialCredentialProvider);

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            Verbose("sdlfkj sdlfkj sdlfkj sdlfkjs d");

            try
            {
                string feedEndPointsJson = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);
                if (string.IsNullOrWhiteSpace(feedEndPointsJson) == false)
                {
                    string uriString = uri.ToString();
                    bool matchingEndpointFound = FindMatchingEndpoint(feedEndPointsJson, uriString);
                    if (matchingEndpointFound == true)
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
                Verbose($"Error ror:{e}");
                return Task.FromResult(false);
            }
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            bool isRetry = request.IsRetry;

            // Should fail on retry because the token is provided through a env var. If fails, retry is not going to help. (?)
            /*if (isRetry)
            {
                var responseWithNoRetry = await HandleRequestAsync(GetNonRetryRequest(request), cancellationToken);
                if (responseWithNoRetry?.ResponseCode == MessageResponseCode.Success)
                {
                    Verbose($"Retry: Responding with success");
                    return responseWithNoRetry;
                }

                Verbose($"Retry: responding with error");
                return new GetAuthenticationCredentialsResponse(
                    Credentials.Username,
                    null,
                    string.Format(Resources.BuildTaskIsRetry, request.Uri.ToString()),
                    authenticationTypes: new List<string>
                    {
                        "Basic"
                    },
                     MessageResponseCode.Error);
            }*/

            Verbose($"Responding with success");
            return this.GetResponse(
                    Credentials.Username,
                    Credentials.Password,
                    "message",
                    MessageResponseCode.Success);
            /*return new GetAuthenticationCredentialsResponse(
                    Credentials.Username,
                    Credentials.Password,
                    null,
                    authenticationTypes: new List<string>
                    {
                        "Basic"
                    },
                    MessageResponseCode.Success);*/
        }

        private GetAuthenticationCredentialsRequest GetNonRetryRequest(GetAuthenticationCredentialsRequest request)
        {
            return new GetAuthenticationCredentialsRequest(request.Uri, isRetry: false, request.IsNonInteractive);
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

        private bool FindMatchingEndpoint(string feedEndPointsJson, string uri)
        {
           // Debugger.Launch();
            // Parse JSON from VSS_NUGET_EXTERNAL_FEED_ENDPOINTS
            Verbose(Resources.ParsingJson);
            JObject feedEndPoints = JObject.Parse(feedEndPointsJson);
            Verbose(Resources.ConvertingType);
            EndpointCredentialsContainer endpointCredentials = feedEndPoints.ToObject<EndpointCredentialsContainer>();
            if (endpointCredentials == null)
            {
                Verbose(Resources.NoEndpointsFound);
                return false;
            }

            Verbose(Resources.EndpointMatchLookup);
            Verbose($"uri: {uri}");//remove

            foreach (EndpointCredentials credentials in endpointCredentials.EndpointCredentials)
            {
                Verbose($"credentials.endpoint: {credentials.Endpoint}");//remove

                if (credentials == null)
                {
                    Verbose(Resources.EndpointParseFailure);
                    break;
                }

                if (credentials.Endpoint.Equals(uri, StringComparison.OrdinalIgnoreCase))
                {
                    Verbose(string.Format(Resources.EndpointCredentialCheck, credentials.Endpoint));

                    if (credentials.Password == null)
                    {
                        Verbose(Resources.CredentialParseFailure);
                        break;
                    }

                    Credentials = credentials;

                    if (credentials.Username == null)
                    {
                        Credentials.Username = this.Username;
                    }
                    else {
                        Verbose("setting username");
                        this.Username = Credentials.Username;
                    }
                    Verbose($"Credentials.Endpoint: {Credentials.Endpoint}");
                    Verbose($"Credentials.Username: {Credentials.Username}");//remove
                    Verbose($"Credentials.Password: password");//remove

                    return true;
                }
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
            Verbose(Resources.NoEndpointMatch);
            return false;
        }
    }
}
