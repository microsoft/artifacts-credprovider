// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Common;
using NuGetCredentialProvider.Logging;

namespace CredentialProvider.Microsoft.Tests.Logging
{
    [TestClass]
    public class LogEveryMessageFileLoggerTests
    {
        private string tempLogFile;

        [TestInitialize]
        public void Setup()
        {
            tempLogFile = Path.GetTempFileName();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(tempLogFile))
            {
                try
                {
                    File.Delete(tempLogFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [TestMethod]
        public void Log_WritesMessageToFile()
        {
            // Arrange
            var logger = new LogEveryMessageFileLogger(tempLogFile);
            var message = "Test message";

            // Act
            logger.Log(LogLevel.Info, allowOnConsole: false, message);

            // Assert
            var logContent = File.ReadAllText(tempLogFile);
            Assert.IsTrue(logContent.Contains("Test message"), "Message should be written to file");
        }

        [TestMethod]
        public void Log_PreservesNonSensitiveContent()
        {
            // Arrange
            var logger = new LogEveryMessageFileLogger(tempLogFile);
            var message = "Successfully authenticated to service";

            // Act
            logger.Log(LogLevel.Info, allowOnConsole: false, message);

            // Assert
            var logContent = File.ReadAllText(tempLogFile);
            Assert.IsTrue(logContent.Contains("Successfully authenticated to service"), "Non-sensitive content should be preserved");
        }

        [TestMethod]
        public void Log_HandlesNullMessage()
        {
            // Arrange
            var logger = new LogEveryMessageFileLogger(tempLogFile);

            // Act & Assert - should not throw
            logger.Log(LogLevel.Verbose, allowOnConsole: false, null);
        }
    }
}