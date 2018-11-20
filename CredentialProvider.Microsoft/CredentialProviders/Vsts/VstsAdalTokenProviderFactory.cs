// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public class VstsAdalTokenProviderFactory : IAdalTokenProviderFactory
    {
        private const string Resource = "499b84ac-1321-427f-aa17-267ca6975798";
        private const string ClientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";
        
        private readonly TokenCache tokenCache;

        public VstsAdalTokenProviderFactory(TokenCache tokenCache)
        {
            this.tokenCache = tokenCache;
        }

        public IAdalTokenProvider Get(string authority)
        {
            return new AdalTokenProvider(authority, Resource, ClientId, tokenCache);
        }
    }
}
