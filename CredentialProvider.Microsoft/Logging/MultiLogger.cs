// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    internal class MultiLogger : List<ILogger>, ILogger
    {
        private LogLevel? minLogLevel = null;
        private readonly object logLock = new();

        public void Log(LogLevel level, bool allowOnConsole, string message)
        {
            ILogger[] loggers;
            lock (logLock)
            {
                loggers = this.ToArray();
            }

            foreach (var logger in loggers)
            {
                logger.Log(level, allowOnConsole, message);
            }
        }

        public void SetLogLevel(LogLevel newLogLevel)
        {
            ILogger[] loggers;
            lock (logLock)
            {
                minLogLevel = newLogLevel;
                loggers = this.ToArray();
            }

            foreach (var logger in loggers)
            {
                logger.SetLogLevel(newLogLevel);
            }
        }

        public new void Add(ILogger logger)
        {
            lock (logLock)
            {
                if (minLogLevel.HasValue)
                {
                    logger.SetLogLevel(minLogLevel.Value);
                }

                base.Add(logger);
            }
        }
    }
}
