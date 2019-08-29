using System.IO;
using NuGet.Common;

namespace NuGetCredentialProvider.Logging
{
    internal class LogEveryMessageFileLogger : ILogger
    {
        private readonly string filePath;

        internal LogEveryMessageFileLogger(string filePath)
        {
            this.filePath = filePath;
        }

        public void Log(LogLevel level, bool allowOnConsole, string message)
        {
            File.AppendAllText(filePath, $"[{level}] {message}\n");
        }

        public void SetLogLevel(LogLevel newLogLevel)
        {
            // Do nothing. Always log.
        }
    }
}