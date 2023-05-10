// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Net.Http;
using Microsoft.Artifacts.Authentication;

namespace NuGetCredentialProvider.Util
{
    public class HttpClientFactory : MsalHttpClientFactory
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
            var httpClient = new HttpClient();
#else
            var httpClient  = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15)
            });
#endif

            httpClientFactory = new(httpClient);
        }
    }
}
