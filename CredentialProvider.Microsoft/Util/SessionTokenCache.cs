// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
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
            this.mutexName = @"Global\" + cacheFilePath.Replace(Path.DirectorySeparatorChar, '_');
        }

        private Dictionary<Uri, string> Cache
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

                                int index = WaitHandle.WaitAny(new WaitHandle[] { mutex, this.cancellationToken.WaitHandle }, -1);

                                if (index == 1)
                                {
                                    logger.Verbose(Resources.CancelMessage);
                                    return new Dictionary<Uri, string>();
                                }
                                else if (index == WaitHandle.WaitTimeout)
                                {
                                    logger.Verbose(Resources.SessionTokenCacheMutexFail);
                                    return new Dictionary<Uri, string>();
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
            get => Cache[key];
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

                                int index = WaitHandle.WaitAny(new WaitHandle[] { mutex, this.cancellationToken.WaitHandle }, -1);

                                if (index == 1)
                                {
                                    logger.Verbose(Resources.CancelMessage);
                                }
                                else if (index == WaitHandle.WaitTimeout)
                                {
                                    logger.Verbose(Resources.SessionTokenCacheMutexFail);
                                }
                            }
                        }
                        catch (AbandonedMutexException)
                        {
                            // If this is thrown, then we hold the mutex.
                        }

                        mutexHeld = true;

                        var cache = Cache;
                        cache[key] = value;
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
            return Cache.ContainsKey(key);
        }

        public bool TryGetValue(Uri key, out string value)
        {
            try
            {
                return Cache.TryGetValue(key, out value);
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

                            int index = WaitHandle.WaitAny(new WaitHandle[] { mutex, this.cancellationToken.WaitHandle }, -1);

                            if (index == 1)
                            {
                                logger.Verbose(Resources.CancelMessage);
                            }
                            else if (index == WaitHandle.WaitTimeout)
                            {
                                logger.Verbose(Resources.SessionTokenCacheMutexFail);
                            }
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        // If this is thrown, then we hold the mutex.
                    }

                    mutexHeld = true;

                    var cache = Cache;
                    cache.Remove(key);
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

        private Dictionary<Uri, string> Deserialize(byte[] data)
        {
            if (data == null)
            {
                return new Dictionary<Uri, string>();
            }

            var serialized = System.Text.Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<Dictionary<Uri, string>>(serialized);
        }

        private byte[] Serialize(Dictionary<Uri, string> data)
        {
            var serialized = JsonConvert.SerializeObject(data);
            return System.Text.Encoding.UTF8.GetBytes(serialized);
        }

        private byte[] ReadFileBytes()
        {
            return EncryptedFile.ReadFileBytes(cacheFilePath, readUnencrypted: true);
        }

        private void WriteFileBytes(byte[] bytes)
        {
            EncryptedFile.WriteFileBytes(cacheFilePath, bytes, writeUnencrypted: true);
        }
    }
}
