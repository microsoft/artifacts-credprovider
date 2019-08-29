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

        public void Log(LogLevel level, bool allowOnConsole, string message)
        {
            foreach (var logger in this)
            {
                logger.Log(level, allowOnConsole, message);
            }
        }

        public void SetLogLevel(LogLevel newLogLevel)
        {
            minLogLevel = newLogLevel;

            foreach (var logger in this)
            {
                logger.SetLogLevel(newLogLevel);
            }
        }

        public new void Add(ILogger logger)
        {
            if (minLogLevel.HasValue)
            {
                logger.SetLogLevel(minLogLevel.Value);
            }

            base.Add(logger);
        }
    }
}
