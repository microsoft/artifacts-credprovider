// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class VstsSessionTokenClient : IVstsSessionTokenClient
    {
        private const string TokenScope = "vso.packaging_write vso.drop_write";

        // Known Azure DevOps SPS hostnames that are trusted to receive bearer tokens.
        // This prevents token exfiltration via a malicious X-VSS-AuthorizationEndpoint header.
        public static readonly string[] AllowedSpsHosts = new[]
        {
            ".vssps.visualstudio.com",             // Azure DevOps production (suffix)
            "vssps.dev.azure.com",                 // Azure DevOps production
            ".vssps.dev.azure.com",                // Azure DevOps production (suffix)
            ".vssps.codeapp.ms",                   // AppFabric
            ".vssps.codedev.ms",                   // DevFabric
            ".vssps.vsts.me",                      // DevFabric
        };

        private static readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly Uri vstsUri;
        private readonly string bearerToken;
        private readonly IAuthUtil authUtil;
        private readonly ILogger logger;

        public VstsSessionTokenClient(Uri vstsUri, string bearerToken, IAuthUtil authUtil, ILogger logger)
        {
            this.vstsUri = vstsUri ?? throw new ArgumentNullException(nameof(vstsUri));
            this.bearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
            this.authUtil = authUtil ?? throw new ArgumentNullException(nameof(authUtil));
            this.logger = logger ?? throw new ArgumentException(nameof(logger));
        }

        private HttpRequestMessage CreateRequest(Uri uri, DateTime? validTo)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var tokenRequest = new VstsSessionToken()
            {
                DisplayName = "Azure DevOps Artifacts Credential Provider",
                Scope = TokenScope,
                ValidTo = validTo
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(tokenRequest, options),
                Encoding.UTF8,
                "application/json");

            return request;
        }

        public async Task<string> CreateSessionTokenAsync(VstsTokenType tokenType, DateTime validTo, CancellationToken cancellationToken)
        {
            var spsEndpoint = await authUtil.GetAuthorizationEndpoint(vstsUri, cancellationToken);
            if (spsEndpoint == null)
            {
                return null;
            }

            if (!IsAllowedSpsEndpoint(spsEndpoint))
            {
                logger.Log(NuGet.Common.LogLevel.Error, true,
                    $"SPS authorization endpoint '{spsEndpoint}' is not a known Azure DevOps host. " +
                    "Aborting session token exchange to prevent bearer token exfiltration.");
                return null;
            }

            var uriBuilder = new UriBuilder(spsEndpoint)
            {
                Query = $"tokenType={tokenType}&api-version=5.0-preview.1"
            };

            uriBuilder.Path = uriBuilder.Path.TrimEnd('/') + "/_apis/Token/SessionTokens";

            var httpClient = HttpClientFactory.Default.GetHttpClient();

            using (var request = CreateRequest(uriBuilder.Uri, validTo))
            using (var response = await httpClient.SendAsync(request, cancellationToken))
            {
                logger.LogResponse(NuGet.Common.LogLevel.Verbose, true, response);

                string serializedResponse;
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    request.Dispose();
                    response.Dispose();

                    logger.Log(NuGet.Common.LogLevel.Verbose, true, "Re-trying with service-defined valid-time.");
                    using (var request2 = CreateRequest(uriBuilder.Uri, validTo: null))
                    using (var response2 = await httpClient.SendAsync(request2, cancellationToken))
                    {
                        response2.EnsureSuccessStatusCode();
                        logger.LogResponse(NuGet.Common.LogLevel.Verbose, true, response2);

                        serializedResponse = await response2.Content.ReadAsStringAsync();
                    }
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    serializedResponse = await response.Content.ReadAsStringAsync();
                }

                var responseToken = JsonSerializer.Deserialize<VstsSessionToken>(serializedResponse, options);

                if (validTo.Subtract(responseToken.ValidTo.Value).TotalHours > 1.0)
                {
                    logger.Log(NuGet.Common.LogLevel.Information, true, $"Requested {validTo} but received {responseToken.ValidTo}");
                }

                return responseToken.Token;
            }
        }

        public static bool IsAllowedSpsEndpoint(Uri endpoint)
        {
            if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return AllowedSpsHosts.Any(host => host.StartsWith(".")
                ? endpoint.Host.EndsWith(host, StringComparison.OrdinalIgnoreCase)
                : endpoint.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
        }
    }

    public enum VstsTokenType
    {
        Compact, // Personal Access Token (PAT)
        SelfDescribing // Session Token (JWT)
    }
}