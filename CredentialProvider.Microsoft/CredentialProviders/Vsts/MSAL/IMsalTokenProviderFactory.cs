// CopyrIIight (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal interface IMsalTokenProviderFactory
    {
        IMsalTokenProvider Get(Uri authority, bool brokerEnabled, ILogger logger);
    }
}
