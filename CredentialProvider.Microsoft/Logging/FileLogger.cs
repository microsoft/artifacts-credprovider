// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.IO;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    internal class FileLogger : LoggerBase
    {
        private readonly string filePath;
        private static readonly object writeLock = new object();
        internal FileLogger(string filePath)
        {
            this.filePath = filePath;
        }

        protected override void WriteLog(LogLevel logLevel, string message)
        {
            lock (writeLock)
            {
                File.AppendAllText(filePath, $"[{logLevel}] {message}\n");
            }
        }
    }
}
