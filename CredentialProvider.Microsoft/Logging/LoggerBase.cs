// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    public abstract class LoggerBase : ILogger
    {
        private LogLevel minLogLevel = LogLevel.Debug;
        private bool allowLogWrites = false;
        private ConcurrentQueue<Tuple<LogLevel, string, DateTime>> bufferedLogs = new ConcurrentQueue<Tuple<LogLevel, string, DateTime>>();
        private readonly bool writesToConsole;

        protected LoggerBase(bool writesToConsole)
        {
            this.writesToConsole = writesToConsole;
        }

        public void Log(LogLevel level, bool allowOnConsole, string message)
        {
            if (writesToConsole && !allowOnConsole)
            {
                return;
            }

            if (!allowLogWrites)
            {
                // cheap reserve, if it swaps out after we add, meh, we miss one log
                var buffer = bufferedLogs;
                if (buffer != null)
                {
                    buffer.Enqueue(Tuple.Create(level, message, DateTime.Now));
                }

                // we could pass this through if buffer is null since the Set message has already come through to unblock us, but
                // the race should be rare and we don't know exactly how nuget will behave with the parallelism so
                // opt to ignore this one racing log message.
                return;
            }
            else if (bufferedLogs != null)
            {
                ConcurrentQueue<Tuple<LogLevel, string, DateTime>> buffer = null;
                buffer = Interlocked.CompareExchange(ref bufferedLogs, null, bufferedLogs);

                if (buffer != null)
                {
                    foreach (var log in buffer)
                    {
                        if (log.Item1 >= minLogLevel)
                        {
                            WriteLog(log.Item1, GetLogPrefix(log.Item3) + log.Item2);
                        }
                    }
                }
            }

            if (level >= minLogLevel)
            {
                WriteLog(level, GetLogPrefix(null) + message);
            }
        }

        public void SetLogLevel(LogLevel newLogLevel)
        {
            minLogLevel = newLogLevel;
            allowLogWrites = true;
        }

        protected abstract void WriteLog(LogLevel logLevel, string message);

        private string GetLogPrefix(DateTime? bufferedLogTime)
        {
            string timeString = bufferedLogTime.HasValue
                ? bufferedLogTime.Value.ToString(".HHmmss")
                : null;

            return $"[CredentialProvider{timeString}]";
        }
    }
}
