// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    public class AuthUtilTests
    {
        private readonly CancellationToken cancellationToken = default(CancellationToken);
        private readonly Uri organizationsAuthority = new Uri("https://login.microsoftonline.com/organizations");
        private readonly Uri testAuthority = new Uri("https://example.aad.authority.com");

        private Mock<ILogger> mockLogger;

        private TestableAuthUtil authUtil;
        private IDisposable environmentLock;

        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();

            this.authUtil = new TestableAuthUtil(mockLogger.Object);
            environmentLock = EnvironmentLock.WaitAsync().Result;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            environmentLock?.Dispose();
        }

        [TestMethod]
        public async Task GetAadAuthorityUri_WithoutAuthenticateHeaders_ReturnsCorrectAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            var authorityUri = await authUtil.GetAadAuthorityUriAsync(requestUri, cancellationToken);

            authorityUri.Should().Be(organizationsAuthority);
        }

        [TestMethod]
        public async Task GetAadAuthorityUri_WithoutAuthenticateHeadersAndPpe_ReturnsCorrectAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.vsts.me/_packaging/feed/nuget/v3/index.json");

            var authorityUri = await authUtil.GetAadAuthorityUriAsync(requestUri, cancellationToken);

            authorityUri.Should().Be(new Uri("https://login.windows-ppe.net/organizations"));
        }

        [TestMethod]
        public async Task GetAadAuthorityUri_WithoutAuthenticateHeadersAndPpeAndPpeOverride_ReturnsCorrectAuthority()
        {
            var ppeUris = new[]
            {
                new Uri("https://example.pkgs.vsts.me/_packaging/feed/nuget/v3/index.json"),
                new Uri("https://example.one.ppe.domain/_packaging/feed/nuget/v3/index.json"),
                new Uri("https://example.two.ppe.domain/_packaging/feed/nuget/v3/index.json"),
                new Uri("https://example.three.ppe.domain/_packaging/feed/nuget/v3/index.json"),
            };

            Environment.SetEnvironmentVariable(EnvUtil.PpeHostsEnvVar, string.Join(";", ppeUris.Select(u => u.Host)));

            foreach (var ppeUri in ppeUris)
            {
                var authorityUri = await authUtil.GetAadAuthorityUriAsync(ppeUri, cancellationToken);

                authorityUri.Should().Be(new Uri("https://login.windows-ppe.net/organizations"));
            }
        }

        [TestMethod]
        public async Task GetAadAuthorityUri_WithAuthenticateHeaders_ReturnsCorrectAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockAadAuthorityHeaders(testAuthority);

            var authorityUri = await authUtil.GetAadAuthorityUriAsync(requestUri, cancellationToken);

            authorityUri.Should().Be(testAuthority);
        }

        [TestMethod]
        public async Task GetAadAuthorityUri_WithAuthenticateHeadersAndEnvironmentOverride_ReturnsOverrideAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");
            var overrideAuthority = new Uri("https://override.aad.authority.com");

            MockAadAuthorityHeaders(testAuthority);

            Environment.SetEnvironmentVariable(EnvUtil.MsalAuthorityEnvVar, overrideAuthority.ToString());
            var authorityUri = await authUtil.GetAadAuthorityUriAsync(requestUri, cancellationToken);

            authorityUri.Should().Be(overrideAuthority);
        }

        [TestMethod]
        public async Task GetFeedUriSource_TenantHeaderNotPresent_ReturnsExternal()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);
            feedSource.Should().Be(AzDevDeploymentType.External);
        }

        [TestMethod]
        public async Task GetFeedUriSource_TenantHeaderPresent_VssAuthorizationEndpointNotPresent_ReturnsExternal()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockVssResourceTenantHeader();

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);
            feedSource.Should().Be(AzDevDeploymentType.External);
        }

        [TestMethod]
        public async Task GetFeedUriSource_AuthorizationEndpointHeaderPresent_TenantHeaderNotPresent_ReturnsExternal()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockVssAuthorizationEndpointHeader();

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);
            feedSource.Should().Be(AzDevDeploymentType.External);
        }

        [TestMethod]
        public async Task GetFeedUriSource_BothHeadersPresent_ReturnsHosted()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockVssResourceTenantHeader();
            MockVssAuthorizationEndpointHeader();

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);
            feedSource.Should().Be(AzDevDeploymentType.Hosted);
        }

        [TestMethod]
        public async Task GetFeedUriSource_NoHttps_ReturnsExternal()
        {
            var requestUri = new Uri("http://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockVssResourceTenantHeader();
            MockVssAuthorizationEndpointHeader();

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);
            feedSource.Should().Be(AzDevDeploymentType.External);
        }

        [TestMethod]
        public async Task GetFeedUriSource_OnPremHeaderPresent_ReturnsOnPrem()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockResponseHeaders(AuthUtil.VssE2EID, "id");

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);
            feedSource.Should().Be(AzDevDeploymentType.OnPrem);
        }

        [TestMethod]
        public async Task GetAuthorizationEndpoint_NoHeader_ReturnsNull()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            var authorizationEndpoint = await authUtil.GetAuthorizationEndpoint(requestUri, cancellationToken);
            authorizationEndpoint.Should().BeNull();
        }

        [TestMethod]
        public async Task GetAuthorizationEndpoint_HeaderPresent_ReturnsEndpoint()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockVssAuthorizationEndpointHeader();

            var authorizationEndpoint = await authUtil.GetAuthorizationEndpoint(requestUri, cancellationToken);
            authorizationEndpoint.Should().NotBeNull();
            string.IsNullOrWhiteSpace(authorizationEndpoint.ToString()).Should().BeFalse();
        }

        private void MockResponseHeaders(string key, string value)
        {
            authUtil.HttpResponseHeaders.Add(key, value);
        }

        private void MockVssResourceTenantHeader()
        {
            MockResponseHeaders(AuthUtil.VssResourceTenant, Guid.NewGuid().ToString());
        }

        private void MockVssAuthorizationEndpointHeader()
        {
            MockResponseHeaders(AuthUtil.VssAuthorizationEndpoint, "https://app.vssps.visualstudio.com");
        }

        private void MockAadAuthorityHeaders(Uri aadAuthority)
        {
            MockResponseHeaders("www-authenticate", $"Bearer authorization_uri={aadAuthority}, Basic realm=\"http://example.com/\", TFS-Federated");
        }

        internal class TestableAuthUtil : AuthUtil
        {
            private readonly HttpResponseMessage httpResponseMessage;

            internal TestableAuthUtil(ILogger logger)
                : base(logger)
            {
                this.httpResponseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
            }

            public HttpResponseHeaders HttpResponseHeaders => this.httpResponseMessage.Headers;

            protected override Task<HttpResponseHeaders> GetResponseHeadersAsync(Uri uri, CancellationToken cancellationToken)
            {
                return Task.FromResult(httpResponseMessage.Headers);
            }
        }
    }
}
