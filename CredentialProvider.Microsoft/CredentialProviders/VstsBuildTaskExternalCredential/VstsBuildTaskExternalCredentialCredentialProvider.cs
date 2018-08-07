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

        public override async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            bool isRetry = request.IsRetry;

            // Should fail on retry because the token is provided through an env var. If fails, retry is not going to help.
            if (isRetry)
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
            }

            Verbose($"Responding with success");
            return new GetAuthenticationCredentialsResponse(
                username: Credentials.Username,
                password: Credentials.Password,
                message: null,
                authenticationTypes: new List<string>
                {
                    "Basic"
                },
                responseCode: MessageResponseCode.Success);
        }

        private GetAuthenticationCredentialsRequest GetNonRetryRequest(GetAuthenticationCredentialsRequest request)
        {
            return new GetAuthenticationCredentialsRequest(request.Uri, isRetry: false, request.IsNonInteractive, false);
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

            foreach (EndpointCredentials credentials in endpointCredentials.EndpointCredentials)
            {
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
                        this.Username = Credentials.Username;
                    }
                    Verbose($"Credentials.Endpoint: {Credentials.Endpoint}");

                    return true;
                }
            }
            Verbose(Resources.NoEndpointMatch);
            return false;
        }
    }
}
