// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Util;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint
{
    public class EndpointCredentials
    {
        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; }
        [JsonPropertyName("username")]
        public string Username { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

    public class EndpointCredentialsContainer
    {
        [JsonPropertyName("endpointCredentials")]
        public EndpointCredentials[] EndpointCredentials { get; set; }
    }

    public sealed class VstsBuildTaskServiceEndpointCredentialProvider : CredentialProviderBase
    {
        private Lazy<Dictionary<string, EndpointCredentials>> LazyCredentials;

        // Dictionary that maps an endpoint string to EndpointCredentials
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

            return Task.FromResult(true);
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Verbose(string.Format(Resources.IsRetry, request.IsRetry));

            string uriString = request.Uri.AbsoluteUri;
            bool endpointFound = Credentials.TryGetValue(uriString, out EndpointCredentials matchingEndpoint);
            if (endpointFound)
            {
                Verbose(string.Format(Resources.BuildTaskEndpointMatchingUrlFound, uriString));
                return GetResponse(
                    matchingEndpoint.Username,
                    matchingEndpoint.Password,
                    null,
                    MessageResponseCode.Success);
            }

            Verbose(string.Format(Resources.BuildTaskEndpointNoMatchingUrl, uriString));
            return GetResponse(
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
                Dictionary<string, EndpointCredentials> credsResult = new Dictionary<string, EndpointCredentials>(StringComparer.OrdinalIgnoreCase);
                EndpointCredentialsContainer endpointCredentials = JsonSerializer.Deserialize<EndpointCredentialsContainer>(feedEndPointsJson);
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

                    if (!Uri.TryCreate(credentials.Endpoint, UriKind.Absolute, out var endpointUri))
                    {
                        Verbose(Resources.EndpointParseFailure);
                        break;
                    }

                    var urlEncodedEndpoint = endpointUri.AbsoluteUri;
                    if (!credsResult.ContainsKey(urlEncodedEndpoint))
                    {
                        credsResult.Add(urlEncodedEndpoint, credentials);
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
