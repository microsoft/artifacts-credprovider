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
        Task<BearerTokenResult> GetAsync(Uri uri, bool isRetry, bool isNonInteractive, bool canShowDialog, CancellationToken cancellationToken);
    }

    public class BearerTokenResult
    {
        public BearerTokenResult(string token, bool obtainedInteractively)
        {
            Token = token;
            ObtainedInteractively = obtainedInteractively;
        }

        public string Token { get; }

        public bool ObtainedInteractively { get; }
    }
}
