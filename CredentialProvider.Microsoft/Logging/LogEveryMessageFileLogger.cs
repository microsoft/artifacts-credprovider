// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    internal class LogEveryMessageFileLogger : ILogger
    {
        private static readonly int Pid = Process.GetCurrentProcess().Id;

        private readonly string filePath;

        internal LogEveryMessageFileLogger(string filePath)
        {
            this.filePath = filePath;
            Log(LogLevel.Minimal, allowOnConsole: false, string.Format(Resources.LogStartsAt, DateTime.UtcNow.ToString("u")));
        }

        public void Log(LogLevel level, bool allowOnConsole, string message)
        {
            try
            {
                for (int i = 0; i < 3; ++i)
                {
                    try
                    {
                        File.AppendAllText(
                            filePath,
                            $"[{DateTime.UtcNow:HH:mm:ss.fff} {Pid,5} {GetLevelKeyword(level),7}] {message}\n");
                        return;
                    }
                    catch (IOException)
                    {
                        // retry IOExceptions a couple of times. Could be another instance (or thread) of the plugin locking the file
                        Thread.Sleep(TimeSpan.FromMilliseconds(20));
                    }
                }
            }
            catch
            {
                // don't crash the process just because logging failed.
            }
        }

        private string GetLevelKeyword(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "Debug";

                case LogLevel.Verbose:
                    return "Verbose";

                case LogLevel.Information:
                    return "Info";

                case LogLevel.Minimal:
                    return "Minimal";

                case LogLevel.Warning:
                    return "Warning";

                case LogLevel.Error:
                    return "Error";

                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        void ILogger.SetLogLevel(LogLevel newLogLevel)
        {
            // no-op. This logger always logs all messages.
        }
    }
}
