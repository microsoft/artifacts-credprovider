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

        internal FileLogger(string filePath)
        {
            this.filePath = filePath;
        }

        protected override void WriteLog(LogLevel logLevel, string message)
        {
            File.AppendAllText(filePath, $"[{logLevel}] {message}\n");
        }
    }
}
