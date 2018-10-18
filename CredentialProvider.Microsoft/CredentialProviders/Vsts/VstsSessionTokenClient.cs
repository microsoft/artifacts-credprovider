// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class VstsSessionTokenClient
    {
        private readonly Uri vstsUri;
        private readonly string bearerToken;
        private readonly IAuthUtil authUtil;

        public VstsSessionTokenClient(Uri vstsUri, string bearerToken, IAuthUtil authUtil)
        {
            this.vstsUri = vstsUri ?? throw new ArgumentNullException(nameof(vstsUri));
            this.bearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
            this.authUtil = authUtil ?? throw new ArgumentNullException(nameof(authUtil));
        }

        public async Task<string> CreateSessionTokenAsync(VstsTokenType tokenType, DateTime validTo, CancellationToken cancellationToken)
        {
            var spsEndpoint = await authUtil.GetAuthorizationEndpoint(vstsUri, cancellationToken);
            if (spsEndpoint == null)
            {
                return null;
            }

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                foreach (var userAgent in Program.UserAgent)
                {
                    httpClient.DefaultRequestHeaders.UserAgent.Add(userAgent);
                }

                var content = new StringContent(
                    JsonConvert.SerializeObject(
                        new VstsSessionToken()
                        {
                            DisplayName = "Azure DevOps Artifacts Credential Provider",
                            Scope = "vso.packaging_write",
                            ValidTo = validTo
                        }),
                    Encoding.UTF8,
                    "application/json");

                var uriBuilder = new UriBuilder(spsEndpoint)
                {
                    Query = $"tokenType={tokenType}&api-version=5.0-preview.1"
                };

                uriBuilder.Path = uriBuilder.Path.TrimEnd('/') + "/_apis/Token/SessionTokens";

                using (var response = await httpClient.PostAsync(uriBuilder.Uri, content, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var serializedResponse = await response.Content.ReadAsStringAsync();
                    var responseToken = JsonConvert.DeserializeObject<VstsSessionToken>(serializedResponse);

                    return responseToken.Token;
                }
            }
        }
    }

    public enum VstsTokenType
    {
        Compact, // Personal Access Token (PAT)
        SelfDescribing // Session Token (JWT)
    }
}
