// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetCredentialProvider.Util;

namespace CredentialProvider.Microsoft.Tests.Util
{
    [TestClass]
    public class RedactionUtilTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Reset redaction flag before each test
            RedactionUtil.ShouldRedact = false;
        }

        [TestMethod]
        public void RedactFeedUrl_WhenRedactionDisabled_ReturnsOriginalUrl()
        {
            // Arrange
            RedactionUtil.ShouldRedact = false;
            var url = "https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json";

            // Act
            var result = RedactionUtil.RedactFeedUrl(url);

            // Assert
            Assert.AreEqual(url, result);
        }

        [TestMethod]
        public void RedactFeedUrl_WhenRedactionEnabled_RedactsAzureDevOpsUrl()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;
            var url = "https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json";

            // Act
            var result = RedactionUtil.RedactFeedUrl(url);

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED_FEED_URL]"));
            Assert.IsFalse(result.Contains("myorg"));
            Assert.IsFalse(result.Contains("myfeed"));
        }

        [TestMethod]
        public void RedactFeedUrl_WhenRedactionEnabled_RedactsVisualStudioUrl()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;
            var url = "https://myorg.pkgs.visualstudio.com/_packaging/myfeed/nuget/v3/index.json";

            // Act
            var result = RedactionUtil.RedactFeedUrl(url);

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED_FEED_URL]"));
            Assert.IsFalse(result.Contains("myorg"));
            Assert.IsFalse(result.Contains("myfeed"));
        }

        [TestMethod]
        public void RedactFeedUrl_WhenRedactionEnabled_RedactsDevAzureComUrl()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;
            var url = "https://dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json";

            // Act
            var result = RedactionUtil.RedactFeedUrl(url);

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED_FEED_URL]"));
            Assert.IsFalse(result.Contains("myorg"));
            Assert.IsFalse(result.Contains("myfeed"));
        }

        [TestMethod]
        public void RedactFeedUrl_WhenRedactionEnabled_RedactsOnPremUrl()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;
            var url = "https://myserver.company.com/tfs/DefaultCollection/_packaging/myfeed/nuget/v3/index.json";

            // Act
            var result = RedactionUtil.RedactFeedUrl(url);

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED_FEED_URL]"));
            Assert.IsFalse(result.Contains("myserver"));
            Assert.IsFalse(result.Contains("DefaultCollection"));
            Assert.IsFalse(result.Contains("myfeed"));
        }

        [TestMethod]
        public void RedactFeedUrl_WithUri_WhenRedactionEnabled_RedactsUrl()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;
            var uri = new Uri("https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json");

            // Act
            var result = RedactionUtil.RedactFeedUrl(uri);

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED_FEED_URL]"));
            Assert.IsFalse(result.Contains("myorg"));
        }

        [TestMethod]
        public void RedactFeedUrl_WithNullUri_ReturnsNull()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;
            Uri uri = null;

            // Act
            var result = RedactionUtil.RedactFeedUrl(uri);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ShouldRedact_CanBeSetAndRetrieved()
        {
            // Arrange & Act
            RedactionUtil.ShouldRedact = true;

            // Assert
            Assert.IsTrue(RedactionUtil.ShouldRedact);

            // Arrange & Act
            RedactionUtil.ShouldRedact = false;

            // Assert
            Assert.IsFalse(RedactionUtil.ShouldRedact);
        }

        [TestMethod]
        public void RedactFeedUrl_WhenRedactionEnabled_RedactsAllUrls()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;

            // Act & Assert - Various feed URLs should all be redacted
            var nugetOrg = RedactionUtil.RedactFeedUrl("https://api.nuget.org/v3/index.json");
            Assert.AreEqual("https://[REDACTED_FEED_URL]", nugetOrg);

            var github = RedactionUtil.RedactFeedUrl("https://nuget.pkg.github.com/myorg/index.json");
            Assert.AreEqual("https://[REDACTED_FEED_URL]", github);

            var myget = RedactionUtil.RedactFeedUrl("https://www.myget.org/F/myfeed/api/v3/index.json");
            Assert.AreEqual("https://[REDACTED_FEED_URL]", myget);

            var privateServer = RedactionUtil.RedactFeedUrl("https://packages.internal.company.com/nuget/v3/index.json");
            Assert.AreEqual("https://[REDACTED_FEED_URL]", privateServer);
        }

        [TestMethod]
        public void RedactPassword_WhenRedactionDisabled_ReturnsOriginalPassword()
        {
            // Arrange
            RedactionUtil.ShouldRedact = false;
            var password = "abc123secretToken";

            // Act
            var result = RedactionUtil.RedactPassword(password);

            // Assert
            Assert.AreEqual(password, result);
        }

        [TestMethod]
        public void RedactPassword_WhenRedactionEnabled_RedactsPassword()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;
            var password = "abc123secretToken";

            // Act
            var result = RedactionUtil.RedactPassword(password);

            // Assert
            Assert.AreEqual("[REDACTED]", result);
            Assert.AreNotEqual(password, result);
        }

        [TestMethod]
        public void RedactPassword_WithNull_ReturnsNull()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;

            // Act
            var result = RedactionUtil.RedactPassword(null);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void RedactPassword_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            RedactionUtil.ShouldRedact = true;

            // Act
            var result = RedactionUtil.RedactPassword("");

            // Assert
            Assert.AreEqual("", result);
        }
    }
}
