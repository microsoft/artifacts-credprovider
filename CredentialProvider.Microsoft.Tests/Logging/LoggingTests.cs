// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Common;
using NuGetCredentialProvider.Logging;
using System.IO;

namespace CredentialProvider.Microsoft.Tests.Logging
{
    [TestClass]
    public class LoggingTests
    {
        Mock<TextWriter> mockWriter = new Mock<TextWriter>();

        [TestMethod]
        public void HumanFriendlyTextWriterLogger_EmitsLogLevelAndMessage()
        {
            mockWriter.Setup(x => x.WriteLine(It.IsAny<string>()));
            HumanFriendlyTextWriterLogger logger = new HumanFriendlyTextWriterLogger(mockWriter.Object, writesToConsole: false);
            logger.SetLogLevel(LogLevel.Error);
            logger.Log(LogLevel.Error, allowOnConsole: true, message: "Something bad happened");
            mockWriter.Verify(x => x.WriteLine("[Error] [CredentialProvider]Something bad happened"));
        }
    }
}
