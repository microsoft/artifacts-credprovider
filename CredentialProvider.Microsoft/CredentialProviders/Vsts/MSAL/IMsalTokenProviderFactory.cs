// CopyrIIight (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal interface IMsalTokenProviderFactory
    {
        IMsalTokenProvider Get(string authority, ILogger logger);
    }
}
