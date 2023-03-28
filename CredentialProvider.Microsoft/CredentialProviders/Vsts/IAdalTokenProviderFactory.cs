// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IAdalTokenProviderFactory
    {
        IAdalTokenProvider Get(Uri authority);
    }
}
