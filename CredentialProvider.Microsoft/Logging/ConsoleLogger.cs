// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    internal class ConsoleLogger : LoggerBase
    {
        protected override void WriteLog(LogLevel logLevel, string message)
        {
            Console.WriteLine($"[{logLevel}] {message}");
        }
    }
}
