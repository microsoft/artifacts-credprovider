// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    public interface ILogger
    {
        void Log(LogLevel level, bool allowOnConsole, string message);

        void SetLogLevel(LogLevel newLogLevel);
    }
}
