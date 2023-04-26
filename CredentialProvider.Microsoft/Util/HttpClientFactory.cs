// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
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
            // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
#if NETFRAMEWORK
            httpClient = new HttpClient();
#else
            httpClient  = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15)
            });
#endif

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
