// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using Resources = NuGetCredentialProvider.Resources;

namespace Microsoft.Azure.KeyVault.Helper
{
    /// <summary>
    /// This exception is thrown when KeyVaultHelper class can't read configuration from 
    /// config file or can't access cerificate store and retrieve certificate for KeyVault access.
    /// </summary>
    [Serializable]
    public class KeyVaultHelperConfigurationException : Exception
    {
        public KeyVaultHelperConfigurationException(string message)
            : base(message)
        {
        }

        public KeyVaultHelperConfigurationException(string message, Exception ex)
            : base(string.Format(Resources.ExceptionSeeInnerExceptionForDetails, message), ex)
        {
        }
    }

}
