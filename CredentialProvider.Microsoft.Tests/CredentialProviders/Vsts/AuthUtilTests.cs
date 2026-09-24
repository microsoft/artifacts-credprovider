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
        public async Task GetAuthorizationInfoAsync_WithoutAuthenticateHeaders_ReturnsCorrectAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            var authInfo = await authUtil.GetAuthorizationInfoAsync(requestUri, cancellationToken);

            authInfo.EntraAuthorityUri.Should().Be(organizationsAuthority);
        }

        [TestMethod]
        public async Task GetAuthorizationInfoAsync_WithoutAuthenticateHeadersAndPpe_ReturnsCorrectAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.vsts.me/_packaging/feed/nuget/v3/index.json");

            var authInfo = await authUtil.GetAuthorizationInfoAsync(requestUri, cancellationToken);

            authInfo.EntraAuthorityUri.Should().Be(new Uri("https://login.windows-ppe.net/organizations"));
        }

        [TestMethod]
        public async Task GetAuthorizationInfoAsync_WithoutAuthenticateHeadersAndPpeAndPpeOverride_ReturnsCorrectAuthority()
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
                var authInfo = await authUtil.GetAuthorizationInfoAsync(ppeUri, cancellationToken);

                authInfo.EntraAuthorityUri.Should().Be(new Uri("https://login.windows-ppe.net/organizations"));
            }
        }

        [TestMethod]
        public async Task GetAuthorizationInfoAsync_WithAuthenticateHeaders_ReturnsCorrectAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockAadAuthorityHeaders(testAuthority);

            var authInfo = await authUtil.GetAuthorizationInfoAsync(requestUri, cancellationToken);

            authInfo.EntraAuthorityUri.Should().Be(testAuthority);
        }

        [TestMethod]
        public async Task GetAuthorizationInfoAsync_WithAuthenticateHeadersAndEnvironmentOverride_ReturnsOverrideAuthority()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");
            var overrideAuthority = new Uri("https://override.aad.authority.com");

            MockAadAuthorityHeaders(testAuthority);

            Environment.SetEnvironmentVariable(EnvUtil.MsalAuthorityEnvVar, overrideAuthority.ToString());
            var authInfo = await authUtil.GetAuthorizationInfoAsync(requestUri, cancellationToken);

            authInfo.EntraAuthorityUri.Should().Be(overrideAuthority);
        }

        [TestMethod]
        public async Task GetAuthorizationInfoAsync_WithTenantHeaders_ReturnsCorrectTenantId()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            var testTenant = Guid.NewGuid();
            MockVssResourceTenantHeader(testTenant);

            var authInfo = await authUtil.GetAuthorizationInfoAsync(requestUri, cancellationToken);

            authInfo.EntraTenantId.Should().Be(testTenant.ToString());
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
        public async Task GetFeedUriSource_UntrustedAuthorizationEndpoint_ReturnsExternal()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");

            MockVssResourceTenantHeader();
            MockResponseHeaders(AuthUtil.VssAuthorizationEndpoint, "https://attacker.example.com");

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);

            feedSource.Should().Be(AzDevDeploymentType.External);
        }

        [TestMethod]
        public async Task GetFeedUriSource_InvalidTenantId_ReturnsExternal()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");
            MockResponseHeaders(AuthUtil.VssResourceTenant, "not-a-guid");
            MockVssAuthorizationEndpointHeader();

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);

            feedSource.Should().Be(AzDevDeploymentType.External);
        }

        [TestMethod]
        public async Task GetFeedUriSource_UnknownFeedHostWithValidHeaders_ReturnsExternal()
        {
            var requestUri = new Uri("https://attacker.example.com/_packaging/feed/nuget/v3/index.json");
            MockVssResourceTenantHeader();
            MockVssAuthorizationEndpointHeader();

            var feedSource = await authUtil.GetAzDevDeploymentType(requestUri);

            feedSource.Should().Be(AzDevDeploymentType.External);
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

        [DataTestMethod]
        [DataRow("https://vssps.visualstudio.com")]
        [DataRow("https://app.vssps.visualstudio.com")]
        [DataRow("https://APP.VSSPS.VISUALSTUDIO.COM")]
        [DataRow("https://wcus0.app.vssps.visualstudio.com")]
        [DataRow("https://vssps.dev.azure.com")]
        [DataRow("https://app.vssps.dev.azure.com")]
        [DataRow("https://wcus0.app.vssps.dev.azure.com")]
        [DataRow("https://org.vssps.visualstudio.com")]
        [DataRow("https://test.vssps.codeapp.ms")]
        [DataRow("https://vsspsext.visualstudio.com")]
        [DataRow("https://vsspsext.dev.azure.com")]
        [DataRow("https://vssps.devppe.azure.com")]
        [DataRow("https://app.vssps.devppe.azure.com")]
        [DataRow("https://vssps.vsallin.net")]
        [DataRow("https://app.vssps.vsallin.net")]
        [DataRow("https://api.vssps.vsts.io")]
        [DataRow("https://vssps.codedev.ms")]
        [DataRow("https://test.vssps.codedev.ms")]
        [DataRow("https://test.vssps.vsts.me")]
        public async Task GetAuthorizationEndpoint_TrustedEndpoint_ReturnsEndpoint(string endpoint)
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");
            MockResponseHeaders(AuthUtil.VssAuthorizationEndpoint, endpoint);

            var authorizationEndpoint = await authUtil.GetAuthorizationEndpoint(requestUri, cancellationToken);
            authorizationEndpoint.Should().NotBeNull();
            string.IsNullOrWhiteSpace(authorizationEndpoint.ToString()).Should().BeFalse();
        }

        [TestMethod]
        [DataRow("https://attacker.example.com")]
        [DataRow("https://attacker.com/capture")]
        [DataRow("https://vssps.visualstudio.com.evil.com")]
        [DataRow("https://notvssps.visualstudio.com")]
        [DataRow("http://app.vssps.visualstudio.com")]
        [DataRow("https://evil.com")]
        [DataRow("https://login.microsoftonline.com")]
        [DataRow("https://dev.azure.com")]
        [DataRow("https://pkgs.dev.azure.com")]
        public async Task GetAuthorizationEndpoint_UntrustedEndpoint_ReturnsNull(string endpoint)
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");
            MockResponseHeaders(AuthUtil.VssAuthorizationEndpoint, endpoint);

            var authorizationEndpoint = await authUtil.GetAuthorizationEndpoint(requestUri, cancellationToken);

            authorizationEndpoint.Should().BeNull();
        }

        [TestMethod]
        public async Task GetAuthorizationEndpoint_FeedHostOverrideDoesNotTrustSpsEndpoint()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");
            var untrustedEndpoint = "https://attacker.example.com";
            Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, new Uri(untrustedEndpoint).Host);
            MockResponseHeaders(AuthUtil.VssAuthorizationEndpoint, untrustedEndpoint);

            try
            {
                var authorizationEndpoint = await authUtil.GetAuthorizationEndpoint(requestUri, cancellationToken);

                authorizationEndpoint.Should().BeNull();
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, null);
            }
        }

        [TestMethod]
        public async Task GetAuthorizationEndpoint_MultipleHeaders_ReturnsNull()
        {
            var requestUri = new Uri("https://example.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json");
            MockVssAuthorizationEndpointHeader();
            MockResponseHeaders(AuthUtil.VssAuthorizationEndpoint, "https://vssps.dev.azure.com");

            var authorizationEndpoint = await authUtil.GetAuthorizationEndpoint(requestUri, cancellationToken);

            authorizationEndpoint.Should().BeNull();
        }

        private void MockResponseHeaders(string key, string value)
        {
            authUtil.HttpResponseHeaders.Add(key, value);
        }

        private void MockVssResourceTenantHeader(Guid? guid = null)
        {
            MockResponseHeaders(AuthUtil.VssResourceTenant, (guid ?? Guid.NewGuid()).ToString());
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
