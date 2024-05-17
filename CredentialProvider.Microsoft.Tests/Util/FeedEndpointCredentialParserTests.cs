// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGetCredentialProvider.Util;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace CredentialProvider.Microsoft.Tests.Util;

[TestClass]
public class FeedEndpointCredentialParserTests
{
    private IDisposable environmentLock;
    private Mock<ILogger> loggerMock;

    public FeedEndpointCredentialParserTests()
    {
        environmentLock = EnvironmentLock.WaitAsync().Result;
        loggerMock = new Mock<ILogger>();
    }

    [TestCleanup]
    public virtual void TestCleanup()
    {
        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, null);
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, null);
        environmentLock?.Dispose();
    }

    [TestMethod]
    public void ParseFeedEndpointsJsonToDictionary_ReturnsCredentials()
    {
        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"clientId\": \"testClientId\"}]}";
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Count.Should().Be(1);
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].ClientId.Should().Be("testClientId");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("invalid json")]
    public void ParseFeedEndpointsJsonToDictionary_WhenInputInvalid_ReturnsEmpty(string invalidInput)
    {
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, invalidInput);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void ParseFeedEndpointsJsonToDictionary_WithNoClientId_ReturnsEmpty()
    {
        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\"}]}";
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void ParseFeedEndpointsJsonToDictionary_WithCertificateFilePath_ReturnsCredentials()
    {
        string feedEndPointJson = @"{""endpointCredentials"":[{""endpoint"":""http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"", ""clientId"": ""testClientId"", ""clientCertificateFilePath"": ""test\\file\\path""}]}";
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Count.Should().Be(1);
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].ClientId.Should().Be("testClientId");
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].CertificateFilePath.Should().Be("test\\file\\path");
    }


    [TestMethod]
    public void ParseFeedEndpointsJsonToDictionary_WithCertificateUnixFilePath_ReturnsCredentials()
    {
        string feedEndPointJson = @"{""endpointCredentials"":[{""endpoint"":""http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"", ""clientId"": ""testClientId"", ""clientCertificateFilePath"": ""test/file/path""}]}";
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].ClientId.Should().Be("testClientId");
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].CertificateFilePath.Should().Be("test/file/path");
    }

    [TestMethod]
    public void ParseFeedEndpointsJsonToDictionary_WithSubjectName_ReturnsCredentials()
    {
        string feedEndPointJson = @"{""endpointCredentials"":[{""endpoint"":""http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"", ""clientId"": ""testClientId"", ""clientCertificateSubjectName"": ""someSubjectName""}]}";
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Count.Should().Be(1);
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].ClientId.Should().Be("testClientId");
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].CertificateSubjectName.Should().Be("someSubjectName");
    }

    [TestMethod]
    public void ParseFeedEndpointsJsonToDictionary_WithCertificateUnixFilePathAndSubjectName_ReturnsEmpty()
    {
        string feedEndPointJson = @"{""endpointCredentials"":[{""endpoint"":""http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"", ""clientId"": ""testClientId"", ""clientCertificateFilePath"": ""test/file/path"", , ""clientCertificateSubjectName"": ""someSubjectName""}]}";
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void ParseFeedEndpointsJsonToDictionary_WhenSingleQuotePresent_ReturnsEmpty()
    {
        string feedEndPointJson = "{'endpointCredentials':['endpoint':'http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json', 'clientId': 'testClientId'}]}";
        Environment.SetEnvironmentVariable(EnvUtil.EndpointCredentials, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void ParseExternalFeedEndpointsJsonToDictionary_ReturnsCredentials()
    {
        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"username\": \"testuser\", \"password\": \"testPassword\"}]}";
        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseExternalFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Count.Should().Be(1);
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].Username.Should().Be("testuser");
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].Password.Should().Be("testPassword");
    }

    [TestMethod]
    public void ParseExternalFeedEndpointsJsonToDictionary_WithoutUserName_ReturnsCredentials()
    {
        string feedEndPointJson = "{\"endpointCredentials\":[{\"endpoint\":\"http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\", \"password\": \"testPassword\"}]}";
        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseExternalFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Count.Should().Be(1);
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].Username.Should().Be("VssSessionToken");
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].Password.Should().Be("testPassword");
    }

    [TestMethod]
    public void ParseExternalFeedEndpointsJsonToDictionary_WithSingleQuotes_ReturnsCredentials()
    {
        string feedEndPointJson = "{\'endpointCredentials\':[{\'endpoint\':\'http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json\', \'username\': \'testuser\', \'password\': \'testPassword\'}]}";
        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, feedEndPointJson);

        var result = FeedEndpointCredentialsParser.ParseExternalFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Count.Should().Be(1);
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].Username.Should().Be("testuser");
        result["http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json"].Password.Should().Be("testPassword");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("invalid json")]
    public void ParseFeedEndpointsJsonToDictionary_WhenInvalidInput_ReturnsEmpty(string input)
    {
        Environment.SetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints, input);

        var result = FeedEndpointCredentialsParser.ParseExternalFeedEndpointsJsonToDictionary(loggerMock.Object);

        result.Should().BeEmpty();
    }
}
