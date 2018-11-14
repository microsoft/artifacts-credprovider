// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Collections.Generic;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IBearerTokenProvidersFactory
    {
        IEnumerable<IBearerTokenProvider> Get(string authority);
    }
}