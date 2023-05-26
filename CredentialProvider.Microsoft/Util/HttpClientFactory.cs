// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.Artifacts.Authentication;
using IAdalHttpClientFactory = Microsoft.IdentityModel.Clients.ActiveDirectory.IHttpClientFactory;

namespace NuGetCredentialProvider.Util
{
    public class HttpClientFactory : MsalHttpClientFactory, IAdalHttpClientFactory
    {
        private static readonly HttpClientFactory httpClientFactory;

        public static HttpClientFactory Default => httpClientFactory;

        public HttpClientFactory(HttpClient httpClient) : base(httpClient)
        {
        }

        static HttpClientFactory()
        {
            // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
#if NETFRAMEWORK
            var httpClient = new HttpClient(new HttpClientHandler
            {
                UseDefaultCredentials = true
            });
#else
            var httpClient  = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                DefaultProxyCredentials = CredentialCache.DefaultCredentials
            });
#endif

            httpClientFactory = new(httpClient);
        }
    }
}
