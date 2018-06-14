// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IBearerTokenProvider
    {
        Task<string> GetAsync(Uri uri, bool isRetry, bool isNonInteractive, CancellationToken cancellationToken);
    }
}
