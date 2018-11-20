// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    public static class AdalTokenCacheUtils
    {
        public static TokenCache GetAdalTokenCache(ILogger logger)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.Verbose(Resources.DPAPIUnavailableNonWindows);
                return TokenCache.DefaultShared;
            }

            if (!EnvUtil.AdalFileCacheEnabled())
            {
                logger.Verbose(Resources.AdalFileCacheDisabled);
                return TokenCache.DefaultShared;
            }

            logger.Verbose(string.Format(Resources.AdalFileCacheLocation, EnvUtil.AdalTokenCacheLocation));
            return new AdalFileCache(EnvUtil.AdalTokenCacheLocation);
        }
    }
}