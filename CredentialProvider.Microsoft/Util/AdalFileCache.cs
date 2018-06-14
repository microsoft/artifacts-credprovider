// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.IO;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NuGetCredentialProvider.Util
{
    internal class AdalFileCache : TokenCache
    {
        private static readonly object FileLock = new object();
        private string cacheFilePath;

        // Initializes the cache against a local file.
        // If the file is already present, it loads its content in the ADAL cache
        public AdalFileCache(string cacheFilePath)
        {
            this.cacheFilePath = cacheFilePath;
            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            lock (FileLock)
            {
                this.Deserialize(this.ReadFileBytes());
            }
        }

        // Empties the persistent store.
        public override void Clear()
        {
            base.Clear();
            File.Delete(cacheFilePath);
        }

        // Triggered right before ADAL needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                this.Deserialize(this.ReadFileBytes());
            }
        }

        // Triggered right after ADAL accessed the cache.
        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (this.HasStateChanged)
            {
                lock (FileLock)
                {
                    // reflect changes in the persistent store
                    this.WriteFileBytes(this.Serialize());

                    // once the write operation took place, restore the HasStateChanged bit to false
                    this.HasStateChanged = false;
                }
            }
        }

        private byte[] ReadFileBytes()
        {
            return EncryptedFile.ReadFileBytes(cacheFilePath, readUnencrypted: false);
        }

        private void WriteFileBytes(byte[] bytes)
        {
            EncryptedFile.WriteFileBytes(cacheFilePath, bytes, writeUnencrypted: false);
        }
    }
}
