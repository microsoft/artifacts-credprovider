// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Net.Http;
using System.Net.Http.Headers;
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
            var httpClient = new HttpClient(new HttpClientHandler
            {
                // This is needed to make IWA work. See:
                // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/e15befb5f0a6cf4757c5abcaa6487e28e7ebd1bb/src/client/Microsoft.Identity.Client/PlatformsCommon/Shared/SimpleHttpClientFactory.cs#LL26C1-L27C73
                UseDefaultCredentials = true
            });

            httpClient.DefaultRequestHeaders.UserAgent.Add(ProgramContext);
            httpClientFactory = new(httpClient);
        }

        private static ProductInfoHeaderValue ProgramContext => 
            new ProductInfoHeaderValue($"({EnvUtil.GetProgramContextFromEnvironment()})");
    }

    public enum Context
    {
        Maven,
        NuGet,
        Pip,
        Conda, 
    }
}
