// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Net.Http;
using Microsoft.Identity.Client;
using IAdalHttpClientFactory = Microsoft.IdentityModel.Clients.ActiveDirectory.IHttpClientFactory;

namespace NuGetCredentialProvider.Util
{
    internal class HttpClientFactory : IMsalHttpClientFactory, IAdalHttpClientFactory
    {
        private static readonly HttpClient httpClient;

        public static HttpClientFactory Default => new();

        static HttpClientFactory()
        {
            httpClient = new HttpClient(new HttpClientHandler
            {
                // This is needed to make IWA work. See:
                // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/e15befb5f0a6cf4757c5abcaa6487e28e7ebd1bb/src/client/Microsoft.Identity.Client/PlatformsCommon/Shared/SimpleHttpClientFactory.cs#LL26C1-L27C73
                UseDefaultCredentials = true
            });

            foreach (var item in Program.UserAgent)
            {
                httpClient.DefaultRequestHeaders.UserAgent.Add(item);
            }
        }

        public HttpClient GetHttpClient()
        {
            return httpClient;
        }
    }
}
