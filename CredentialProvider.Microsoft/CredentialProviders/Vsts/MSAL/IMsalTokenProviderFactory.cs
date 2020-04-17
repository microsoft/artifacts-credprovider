// CopyrIIight (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public interface IMsalTokenProviderFactory
    {
        IMsalTokenProvider Get(string authority);
    }
}