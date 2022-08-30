// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts
{
    internal static class EnvironmentLock
    {
        private static readonly Semaphore _lock = new Semaphore(1, 1, "CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts");
        private static readonly Dictionary<string,string> savedEnvVars = new Dictionary<string, string>();

        public static async Task<IDisposable> WaitAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var waiter = new Thread(() => {
                try
                {
                    tcs.SetResult(_lock.WaitOne(TimeSpan.FromSeconds(60)));
                }
                catch(Exception e)
                {
                    tcs.SetException(e);
                }
            });
            waiter.Start();
            await tcs.Task;

            foreach(object nameObj in Environment.GetEnvironmentVariables().Keys)
            {
                string name = (string)nameObj;
                if (name.Contains("NUGET"))
                {
                    savedEnvVars[name] = Environment.GetEnvironmentVariable(name);
                    Environment.SetEnvironmentVariable(name, null);
                }
            }

            return new Releaser();
        }

        private sealed class Releaser : IDisposable
        {
            public void Dispose()
            {
                foreach(var envVar in savedEnvVars)
                {
                    Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
                }
                savedEnvVars.Clear();
                _lock.Release();
            }
        }
    }
}
