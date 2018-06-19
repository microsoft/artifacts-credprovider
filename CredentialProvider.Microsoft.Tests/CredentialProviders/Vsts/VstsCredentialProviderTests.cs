// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts
{
    [TestClass]
    public class VstsCredentialProviderTests
    {
        private readonly Uri testAuthority = new Uri("https://example.aad.authority.com");

        private Mock<ILogger> mockLogger;
        private Mock<IBearerTokenProvider> mockBearerTokenProvider;
        private Mock<IAuthUtil> mockAuthUtil;

        private VstsCredentialProvider vstsCredentialProvider;

        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();

            mockBearerTokenProvider = new Mock<IBearerTokenProvider>();

            mockAuthUtil = new Mock<IAuthUtil>();
            mockAuthUtil
                .Setup(x => x.GetAadAuthorityUriAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(testAuthority));

            vstsCredentialProvider = new VstsCredentialProvider(mockLogger.Object, mockAuthUtil.Object, mockBearerTokenProvider.Object);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsTrueForKnownSources()
        {
            var sources = new[]
            {
                @"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json",
                @"https://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json",
                @"https://example.pkgs.codedev.ms/_packaging/TestFeed/nuget/v3/index.json",
                @"https://example.pkgs.codeapp.ms/_packaging/TestFeed/nuget/v3/index.json",
                @"https://example.pkgs.visualstudio.com/_packaging/TestFeed/nuget/v3/index.json",
                @"https://example.pkgs.codex.azure.com/_packaging/TestFeed/nuget/v3/index.json",
            };

            foreach (var source in sources)
            {
                var sourceUri = new Uri(source);
                var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
                canProvideCredentials.Should().BeTrue($"because {source} is a known host");
            }

            mockAuthUtil
                .Verify(x => x.IsVstsUriAsync(It.IsAny<Uri>()), Times.Never, "because we shouldn't probe for known sources");
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsTrueForOverridenSources()
        {
            var sources = new[]
            {
                new Uri(@"http://example.overrideOne.com/_packaging/TestFeed/nuget/v3/index.json"),
                new Uri(@"https://example.overrideTwo.com/_packaging/TestFeed/nuget/v3/index.json"),
                new Uri(@"https://example.overrideThre.com/_packaging/TestFeed/nuget/v3/index.json"),
            };

            Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, string.Join(";", sources.Select(x => x.Host)));
            foreach (var source in sources)
            {
                var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(source);
                canProvideCredentials.Should().BeTrue($"because {source} is an overriden host");
            }

            mockAuthUtil
                .Verify(x => x.IsVstsUriAsync(It.IsAny<Uri>()), Times.Never, "because we shouldn't probe for known sources");
        }
    }
}
