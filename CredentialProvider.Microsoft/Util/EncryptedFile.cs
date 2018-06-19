// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;

namespace NuGetCredentialProvider.Util
{
    public class EncryptedFile
    {
        public static byte[] ReadFileBytes(string filePath, bool readUnencrypted = false)
        {
            try
            {
                return File.Exists(filePath) ? ProtectedData.Unprotect(File.ReadAllBytes(filePath), null, DataProtectionScope.CurrentUser) : null;
            }
            catch (NotSupportedException)
            {
                if (readUnencrypted)
                {
                    return File.Exists(filePath) ? File.ReadAllBytes(filePath) : null;
                }

                throw;
            }
        }

        public static void WriteFileBytes(string filePath, byte[] bytes, bool writeUnencrypted = false)
        {
            try
            {
                EnsureDirectoryExists(filePath);

                File.WriteAllBytes(filePath, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
            }
            catch (NotSupportedException)
            {
                if (writeUnencrypted)
                {
                    File.WriteAllBytes(filePath, bytes);
                    return;
                }

                throw;
            }
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
