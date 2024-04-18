// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts;
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
        
        private IDisposable environmentLock;

        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();

            vstsCredentialProvider = new VstsBuildTaskServiceEndpointCredentialProvider(mockLogger.Object);
            environmentLock = EnvironmentLock.WaitAsync().Result;
        }

        [TestCleanup]
        public virtual void TestCleanup()
        {
            Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, null);
            Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, null);
            environmentLock?.Dispose();
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsFalseForWhenEnvVarIsNotSet()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJsonEnvVarold = EnvUtil.BuildTaskExternalEndpoints;
            string feedEndPointJsonEnvVarnew = EnvUtil.EndpointCredentials;

            // Setting environment variable to null
            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVarold, null);
            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVarnew, null);

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(false, result);
        }

        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public async Task CanProvideCredentials_ReturnsTrueForCorrectEnvironmentVariable(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(true, result);
        }

        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public async Task HandleRequestAsync_ReturnsSuccess(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Success);
            Assert.AreEqual(result.Username, "testUser");
            Assert.AreEqual(result.Password, "testToken");
        }

        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public async Task HandleRequestAsync_ReturnsSuccessWhenSingleQuotesInJson(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJson = "{\'endpointCredentials\':[{\'endpoint\':\'http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\', \'username\': \'testUser\', \'password\':\'testToken\'}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Success);
            Assert.AreEqual(result.Username, "testUser");
            Assert.AreEqual(result.Password, "testToken");
        }

        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public async Task HandleRequestAsync_ReturnsSuccessWhenMultipleSourcesInJson(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://example3.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");

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

        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public async Task HandleRequestAsync_ReturnsErrorWhenMatchingEndpointIsNotFound(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://exampleThatDoesNotMatch.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);
            
            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
        }

        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public void HandleRequestAsync_ThrowsWithInvalidJson(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string invalidFeedEndPointJson = "this is not json";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, invalidFeedEndPointJson);

            Func<Task> act = async () => await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            act.Should().Throw<Exception>();
        }

        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public async Task HandleRequestAsync_MatchesEndpointURLCaseInsensitive(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_Packaging/TestFEED/nuget/v3/index.json");

            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(MessageResponseCode.Success, result.ResponseCode);
            Assert.AreEqual("testUser", result.Username);
            Assert.AreEqual("testToken", result.Password);
        }


        [DataTestMethod]
        [DataRow(EnvUtil.BuildTaskExternalEndpoints)]
        [DataRow(EnvUtil.EndpointCredentials)]
        public async Task HandleRequestAsync_MatchesEndpointURLWithSpaces(string feedEndPointJsonEnvVar)
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/My Collection/_packaging/TestFeed/nuget/v3/index.json");

            string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/My Collection/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

            Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(MessageResponseCode.Success, result.ResponseCode);
            Assert.AreEqual("testUser", result.Username);
            Assert.AreEqual("testToken", result.Password);
        }
    }
}
