// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IAdalTokenProviderFactory
    {
        IAdalTokenProvider Get(string authority);
    }

    public class AdalTokenProviderFactory : IAdalTokenProviderFactory
    {
        private readonly string resource;
        private readonly string clientId;
        private readonly TokenCache tokenCache;

        public AdalTokenProviderFactory(string resource, string clientId, TokenCache tokenCache)
        {
            this.resource = resource;
            this.clientId = clientId;
            this.tokenCache = tokenCache;
        }

        public IAdalTokenProvider Get(string authority)
        {
            return new AdalTokenProvider(authority, resource, clientId, tokenCache);
        }
    }
}
