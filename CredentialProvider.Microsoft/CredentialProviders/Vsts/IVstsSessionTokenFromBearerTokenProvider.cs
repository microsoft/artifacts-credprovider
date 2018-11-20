// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IAzureDevOpsSessionTokenFromBearerTokenProvider
    {
        Task<string> GetAzureDevOpsSessionTokenFromBearerToken(
            GetAuthenticationCredentialsRequest request,
            string bearerToken,
            bool bearerTokenObtainedInteractively,
            CancellationToken cancellationToken);
    }
}