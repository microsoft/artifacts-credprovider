// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders.VstsBuildTask;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.VstsBuildTask
{ 
    [TestClass]
    public class VstsBuildTaskCredentialProviderTests
    {
        private readonly Uri testAuthority = new Uri("https://example.aad.authority.com");

        private Mock<ILogger> mockLogger;

        private VstsBuildTaskCredentialProvider vstsCredentialProvider;

        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();

            vstsCredentialProvider = new VstsBuildTaskCredentialProvider(mockLogger.Object);
        }

        [TestCleanup]
        public virtual void TestCleanup()
        {
            string uriPrefixesEnvVar = EnvUtil.BuildTaskUriPrefixes;
            string accessTokenEnvVar = EnvUtil.BuildTaskAccessToken;
            Environment.SetEnvironmentVariable(uriPrefixesEnvVar, null);
            Environment.SetEnvironmentVariable(accessTokenEnvVar, null);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsFalseWhenEnvironmentVariablesAreNotSet()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string uriPrefixesEnvVar = EnvUtil.BuildTaskUriPrefixes;
            string accessTokenEnvVar = EnvUtil.BuildTaskAccessToken;

            // Setting environment variables to null
            Environment.SetEnvironmentVariable(uriPrefixesEnvVar, null);
            Environment.SetEnvironmentVariable(accessTokenEnvVar, null);

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsTrueForCorrectEnvironmentVariableAndMatchingSourceUri()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string uriPrefixesEnvVar = EnvUtil.BuildTaskUriPrefixes;
            string accessTokenEnvVar = EnvUtil.BuildTaskAccessToken;

            Environment.SetEnvironmentVariable(uriPrefixesEnvVar, "http://example.pkgs.vsts.me/");
            Environment.SetEnvironmentVariable(accessTokenEnvVar, "accessToken");

            var result = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public async Task HandleRequestAsync_ReturnsSuccess()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string uriPrefixesEnvVar = EnvUtil.BuildTaskUriPrefixes;
            string accessTokenEnvVar = EnvUtil.BuildTaskAccessToken;

            Environment.SetEnvironmentVariable(uriPrefixesEnvVar, "http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            Environment.SetEnvironmentVariable(accessTokenEnvVar, "accessToken");

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Success);
        }

        [TestMethod]
        public async Task HandleRequestAsync_ReturnsErrorWhenPrefixDoesNotMatch()
        {
            Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
            string uriPrefixesEnvVar = EnvUtil.BuildTaskUriPrefixes;
            string accessTokenEnvVar = EnvUtil.BuildTaskAccessToken;

            Environment.SetEnvironmentVariable(uriPrefixesEnvVar, "http://urlThatDoesNotMatch");
            Environment.SetEnvironmentVariable(accessTokenEnvVar, "accessToken");

            var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
            Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
        }
    }
}
