// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Protocol.Plugins;
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
        public async Task CanProvideCredentials_ReturnsTrueForCorrectEnvironmentVariable()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public async Task HandleRequestAsync_ReturnsSuccess()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Success);
            Assert.AreEqual(result.Username, "testUser");
            Assert.AreEqual(result.Password, "testToken");
        }

        [TestMethod]
        public async Task HandleRequestAsync_ReturnsSuccessWhenMultipleSourcesInJson()
        {
            Uri sourceUri = new Uri(@"http://example3.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;

            string feedEndPointJson = "{\"endpointCredentials\":[" +
                "{\"endpoint\":\"http://example1.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser1\", \"password\":\"testToken1\"}, " +
                "{\"endpoint\":\"http://example2.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser2\", \"password\":\"testToken2\"}, " +
                "{\"endpoint\":\"http://example3.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser3\", \"password\":\"testToken3\"}, " +
                "{\"endpoint\":\"http://example4.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser4\", \"password\":\"testToken4\"}" +
                "]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Success);
            Assert.AreEqual(result.Username, "testUser3");
            Assert.AreEqual(result.Password, "testToken3");
        }

        [TestMethod]
        public async Task HandleRequestAsync_ReturnsErrorWhenMatchingEndpointIsNotFound()
        {
            Uri sourceUri = new Uri(@"http://exampleThatDoesNotMatch.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);
            
            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
        }

        [TestMethod]
        public void HandleRequestAsync_ThrowsWithInvalidJson()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;
            string invalidFeedEndPointJson = "this is not json";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, invalidFeedEndPointJson);

            Func<Task> act = async () => await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            act.Should().Throw<Exception>();
        }

        [TestMethod]
        public async Task HandleRequestAsync_MatchesEndpointURLCaseInsensitive()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_Packaging/TestFEED/nuget/v3/index.json");

            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(MessageResponseCode.Success, result.ResponseCode);
            Assert.AreEqual("testUser", result.Username);
            Assert.AreEqual("testToken", result.Password);
        }


        [TestMethod]
        public async Task HandleRequestAsync_MatchesEndpointURLWithSpaces()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/My Collection/_packaging/TestFeed/nuget/v3/index.json");

            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/My Collection/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";
            string feedEndPointJsonEnvVar = EnvUtil.BuildTaskExternalEndpoints;

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(MessageResponseCode.Success, result.ResponseCode);
            Assert.AreEqual("testUser", result.Username);
            Assert.AreEqual("testToken", result.Password);
        }
    }
}
