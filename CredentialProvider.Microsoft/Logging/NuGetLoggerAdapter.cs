// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging;

internal class NuGetLoggerAdapter : Microsoft.Extensions.Logging.ILogger
{
    private readonly ILogger logger;
    private readonly LogLevel logLevel;

    public NuGetLoggerAdapter(ILogger logger, LogLevel logLevel)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.logLevel = logLevel;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return GetNuGetLogLevel(logLevel) >= this.logLevel;
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        logger.Log(GetNuGetLogLevel(logLevel), true, formatter(state, exception));
    }

    private LogLevel GetNuGetLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        switch (logLevel)
        {
            // Seems backwards, but NuGet defines a different order for log levels
            case Microsoft.Extensions.Logging.LogLevel.Trace:
                return LogLevel.Debug;
            case Microsoft.Extensions.Logging.LogLevel.Debug:
                return LogLevel.Verbose;
            case Microsoft.Extensions.Logging.LogLevel.Information:
                return LogLevel.Information;
            case Microsoft.Extensions.Logging.LogLevel.Warning:
                return LogLevel.Warning;
            case Microsoft.Extensions.Logging.LogLevel.Error:
                return LogLevel.Error;
            case Microsoft.Extensions.Logging.LogLevel.Critical:
                return LogLevel.Error;
            case Microsoft.Extensions.Logging.LogLevel.None:
                return LogLevel.Error;
            default:
                return LogLevel.Minimal;
        }
    }
}
