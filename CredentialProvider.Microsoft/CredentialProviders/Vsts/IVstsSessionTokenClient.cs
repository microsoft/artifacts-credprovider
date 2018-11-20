// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IVstsSessionTokenClient
    {
        Task<string> CreateSessionTokenAsync(VstsTokenType tokenType, DateTime validTo, CancellationToken cancellationToken);
    }
}