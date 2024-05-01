// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts;
using Microsoft.Artifacts.Authentication;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.VstsBuildTaskServiceEndpoint;

[TestClass]
public class VstsBuildTaskServiceEndpointCredentialProviderTests
{

    private Mock<ILogger> mockLogger;

    private Mock<ITokenProvidersFactory> mockTokenProviderFactory;

    private VstsBuildTaskServiceEndpointCredentialProvider vstsCredentialProvider;

    private IDisposable environmentLock;

    [TestInitialize]
    public void TestInitialize()
    {
        mockLogger = new Mock<ILogger>();
        var mockAuthUtil = new Mock<IAuthUtil>();
        mockTokenProviderFactory = new Mock<ITokenProvidersFactory>();

        vstsCredentialProvider = new VstsBuildTaskServiceEndpointCredentialProvider(
            mockLogger.Object,
            mockTokenProviderFactory.Object,
            mockAuthUtil.Object);
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

    [TestMethod]
    public async Task HandleRequestAsync_WithExternalEndpoint_ReturnsSuccess()
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
    public async Task HandleRequestAsync_WithEndpoint_ReturnsSuccess()
    {
        Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
        string feedEndPointJsonEnvVar = EnvUtil.EndpointCredentials;
        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"clientId\": \"testClientId\"}]}";

        Environment.SetEnvironmentVariable(feedEndPointJsonEnvVar, feedEndPointJson);
        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, null);

        mockTokenProviderFactory.Setup(x =>
            x.GetAsync(It.IsAny<Uri>()))
                .ReturnsAsync(new List<ITokenProvider>()
                {
                    SetUpMockManagedIdentityTokenProvider("someTokenValue").Object
                });

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(result.ResponseCode, MessageResponseCode.Success);
        Assert.AreEqual(result.Username, "testClientId");
        Assert.AreEqual(result.Password, "someTokenValue");
    }

    [TestMethod]
    public async Task HandleRequestAsync_ExternalEndpoints_ReturnsSuccessWhenMultipleSourcesInJson()
    {
        Uri sourceUri = new Uri(@"http://example3.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");

        string feedEndPointJson = "{\"endpointCredentials\":[" +
            "{\"endpoint\":\"http://example1.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser1\", \"password\":\"testToken1\"}, " +
            "{\"endpoint\":\"http://example2.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser2\", \"password\":\"testToken2\"}, " +
            "{\"endpoint\":\"http://example3.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser3\", \"password\":\"testToken3\"}, " +
            "{\"endpoint\":\"http://example4.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser4\", \"password\":\"testToken4\"}" +
            "]}";

        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, feedEndPointJson);

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(result.ResponseCode, MessageResponseCode.Success);
        Assert.AreEqual(result.Username, "testUser3");
        Assert.AreEqual(result.Password, "testToken3");
    }

    [TestMethod]
    public async Task HandleRequestAsync_ExternalEndpoints_ReturnsErrorWhenMatchingEndpointIsNotFound()
    {
        Uri sourceUri = new Uri(@"http://exampleThatDoesNotMatch.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, feedEndPointJson);

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
    }

    [TestMethod]
    public async Task HandleRequestAsync_Endpoints_ReturnsErrorWhenMatchingEndpointIsNotFound()
    {
        Uri sourceUri = new Uri(@"http://exampleThatDoesNotMatch.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"clientId\": \"someClientId\"}]}";

        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
    }

    [TestMethod]
    public async Task HandleRequestAsync_MatchesEndpointURLCaseInsensitive()
    {
        Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_Packaging/TestFEED/nuget/v3/index.json");

        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"password\":\"testToken\"}]}";

        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, feedEndPointJson);

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

        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, feedEndPointJson);

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(MessageResponseCode.Success, result.ResponseCode);
        Assert.AreEqual("testUser", result.Username);
        Assert.AreEqual("testToken", result.Password);
    }

    [TestMethod]
    public async Task HandleRequestAsync_WithInvalidBearer_ReturnsError()
    {
        Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
        string feedEndPointJson = $"{{\"endpointCredentials\":[{{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testUser\", \"azureClientId\":\"\"}}]}}";

        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        mockTokenProviderFactory.Setup(x =>
            x.GetAsync(It.IsAny<Uri>()))
                .ReturnsAsync(new List<ITokenProvider>() { 
                    SetUpMockManagedIdentityTokenProvider(null).Object });

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
    }

    [TestMethod]
    public async Task HandleRequestAsync_WithNoTokenProvider_ReturnsError()
    {
        Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
        string feedEndPointJson = $"{{\"endpointCredentials\":[{{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\",\"clientId\":\"someClientId\"}}]}}";

        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var mockTokenProvider = new Mock<ITokenProvider>();
        mockTokenProvider.Setup(x => x.Name).Returns("wrong name");

        mockTokenProviderFactory.Setup(x =>
            x.GetAsync(It.IsAny<Uri>()))
                .ReturnsAsync(new List<ITokenProvider>() { mockTokenProvider.Object });

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
    }

    [TestMethod]
    public async Task HandleRequestAsync_OnTokenProviderError_ReturnsError()
    {
        Uri sourceUri = new Uri(@"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
        string feedEndPointJson = $"{{\"endpointCredentials\":[{{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"clientId\":\"\"}}]}}";

        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var mockTokenProvider = new Mock<ITokenProvider>();
        mockTokenProvider.Setup(x => x.Name).Returns("MSAL Managed Identity");
        mockTokenProvider.Setup(x => x.IsInteractive).Returns(false);
        mockTokenProvider.Setup(x => x.CanGetToken(It.IsAny<TokenRequest>())).Returns(true);
        mockTokenProvider.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("some Message"));

        mockTokenProviderFactory.Setup(x =>
            x.GetAsync(It.IsAny<Uri>()))
                .ReturnsAsync(new List<ITokenProvider>() { mockTokenProvider.Object });

        var result = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(sourceUri, false, false, false), CancellationToken.None);
        Assert.AreEqual(result.ResponseCode, MessageResponseCode.Error);
    }

    private static Mock<ITokenProvider> SetUpMockManagedIdentityTokenProvider(string token)
    {
        var mockTokenProvider = new Mock<ITokenProvider>();
        mockTokenProvider.Setup(x => x.Name).Returns("MSAL Managed Identity");
        mockTokenProvider.Setup(x => x.IsInteractive).Returns(false);
        mockTokenProvider.Setup(x => x.CanGetToken(It.IsAny<TokenRequest>())).Returns(true);
        mockTokenProvider.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationResult(
                token,
                false,
                null,
                DateTimeOffset.MinValue,
                DateTimeOffset.MaxValue,
                null,
                Mock.Of<IAccount>(),
                null,
                new List<string>() { },
                Guid.Empty));

        return mockTokenProvider;
    }
}
