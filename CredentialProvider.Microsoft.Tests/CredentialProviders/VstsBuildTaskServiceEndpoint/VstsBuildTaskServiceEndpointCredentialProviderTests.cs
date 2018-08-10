// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.VstsBuildTaskServiceEndpoint
{
    [TestClass]
    public class VstsBuildTaskServiceEndpointCredentialProviderTests
    {
        private readonly Uri feedUri = new Uri("https://example.aad.authority.com");

        private Mock<ILogger> mockLogger;

        private VstsBuildTaskServiceEndpointCredentialProvider vstsCredentialProvider;

        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();

            vstsCredentialProvider = new VstsBuildTaskServiceEndpointCredentialProvider(mockLogger.Object);
        }

        [TestCleanup]
        public virtual void TestCleanup()
        {
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, null);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsFalseForWhenEnvVarIsNotSet()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;

            // Setting environment variable to null
            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, null);

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsTrueForCorrectEnvironmentVariableAndMatchingSourceUri()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsFalseWhenMatchingEndpointIsNotFound()
        {
            Uri sourceUri = new Uri(@"http://exampleThatDoesNotMatch.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ThrowsWithInvalidJson()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            string invalidFeedEndPointJson = "this is not json";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, invalidFeedEndPointJson);

            Action act = () => vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            act.Should().Throw<Exception>();
        }
    }
}
