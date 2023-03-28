// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Linq;
using System.Net.Http;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    public interface ILogger
    {
        void Log(LogLevel level, bool allowOnConsole, string message);

        void SetLogLevel(LogLevel newLogLevel);
    }

    public static class LoggerExtensions
    {
        public static void LogResponse(this ILogger logger, LogLevel level, bool allowOnConsole, HttpResponseMessage response)
        {
            logger.Log(NuGet.Common.LogLevel.Verbose, true, $"Response: {response.StatusCode}");
            if (response.Headers.TryGetValues("ActivityId", out var activityIds))
            {
                string activityId = activityIds.FirstOrDefault();
                if (activityId != null)
                {
                    logger.Log(NuGet.Common.LogLevel.Verbose, true, $" ActivityId: {activityId}");
                }
            }
        }
    }
}
