﻿// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.Util
{
    public class SessionTokenCache : ICache<Uri, string>
    {
        private static readonly object FileLock = new object();
        private readonly string cacheFilePath;
        private ILogger logger;
        private CancellationToken cancellationToken;
        private readonly string mutexName;

        public SessionTokenCache(string cacheFilePath, ILogger logger, CancellationToken cancellationToken)
        {
            this.cacheFilePath = cacheFilePath;
            this.logger = logger;
            this.cancellationToken = cancellationToken;
            this.mutexName = @"Global\" + cacheFilePath.Replace(Path.DirectorySeparatorChar, '_');
        }

        private Dictionary<string, string> Cache
        {
            get
            {
                bool mutexHeld = false, dummy;
                using (Mutex mutex = new Mutex(false, mutexName, out dummy))
                {
                    try
                    {
                        try
                        {
                            if (!mutex.WaitOne(0))
                            {
                                // We couldn't get the mutex on our first acquisition attempt. Log this so the user knows what we're
                                // waiting on.
                                logger.Verbose(Resources.SessionTokenCacheMutexMiss);
                                while (!mutex.WaitOne(100))
                                {
                                    if (this.cancellationToken.IsCancellationRequested)
                                    {
                                        logger.Verbose(Resources.SessionTokenCacheCancelMessage);
                                        return new Dictionary<string, string>();
                                    }
                                }
                            }
                        }
                        catch (AbandonedMutexException)
                        {
                            // If this is thrown, then we hold the mutex.
                        }

                        mutexHeld = true;

                        return Deserialize(ReadFileBytes());
                    }
                    finally
                    {
                        if (mutexHeld)
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                }
            }
        }

        public string this[Uri key]
        {
            get => Cache[key.ToString()];
            set
            {
                bool mutexHeld = false, dummy;
                using (Mutex mutex = new Mutex(false, mutexName, out dummy))
                {
                    try
                    {
                        try
                        {
                            if (!mutex.WaitOne(0))
                            {
                                // We couldn't get the mutex on our first acquisition attempt. Log this so the user knows what we're
                                // waiting on.
                                logger.Verbose(Resources.SessionTokenCacheMutexMiss);
                                while (!mutex.WaitOne(100))
                                {
                                    if (this.cancellationToken.IsCancellationRequested)
                                    {
                                        logger.Verbose(Resources.SessionTokenCacheCancelMessage);
                                        return;
                                    }
                                }
                            }
                        }
                        catch (AbandonedMutexException)
                        {
                            // If this is thrown, then we hold the mutex.
                        }

                        mutexHeld = true;

                        var cache = Cache;
                        cache[key.ToString()] = value;
                        WriteFileBytes(Serialize(cache));
                    }
                    finally
                    {
                        if (mutexHeld)
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                }
            }
        }

        public bool ContainsKey(Uri key)
        {
            return Cache.ContainsKey(key.ToString());
        }

        public bool TryGetValue(Uri key, out string value)
        {
            try
            {
                return Cache.TryGetValue(key.ToString(), out value);
            }
            catch (Exception e)
            {
                if (File.Exists(cacheFilePath))
                {
                    File.Delete(cacheFilePath);
                }

                logger.Verbose(string.Format(Resources.CacheException, e.Message));
                Cache.Clear();
                value = null;

                return false;
            }
        }

        public void Remove(Uri key)
        {
            bool mutexHeld = false, dummy;
            using (Mutex mutex = new Mutex(false, mutexName, out dummy))
            {
                try
                {
                    try
                    {
                        if (!mutex.WaitOne(0))
                        {
                            // We couldn't get the mutex on our first acquisition attempt. Log this so the user knows what we're
                            // waiting on.
                            logger.Verbose(Resources.SessionTokenCacheMutexMiss);
                            while (!mutex.WaitOne(100))
                            {
                                if (this.cancellationToken.IsCancellationRequested)
                                {
                                    logger.Verbose(Resources.SessionTokenCacheCancelMessage);
                                    return;
                                }
                            }
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        // If this is thrown, then we hold the mutex.
                    }

                    mutexHeld = true;

                    var cache = Cache;
                    cache.Remove(key.ToString());
                    WriteFileBytes(Serialize(cache));
                }
                finally
                {
                    if (mutexHeld)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private Dictionary<string, string> Deserialize(byte[] data)
        {
            if (data == null)
            {
                return new Dictionary<string, string>();
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(data);
        }

        private byte[] Serialize(Dictionary<string, string> data)
        {
            return JsonSerializer.SerializeToUtf8Bytes(data);
        }

        private byte[] ReadFileBytes()
        {
            return EncryptedFileWithPermissions.ReadFileBytes(cacheFilePath, readUnencrypted: true);
        }

        private void WriteFileBytes(byte[] bytes)
        {
            try
            {
                EncryptedFileWithPermissions.WriteFileBytes(cacheFilePath, bytes, writeUnencrypted: true);
            } 
            catch(Exception e)
            {
                logger.Verbose(string.Format(Resources.SessionTokenCacheWriteFail, e.GetType(), e.Message));
            }
        }
    }
}
