// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Artifacts.Authentication;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts
{
    [TestClass]
    public class VstsCredentialProviderTests
    {
        private readonly Uri testUri = new Uri("https://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");
        private readonly Uri testAuthority = new Uri("https://example.aad.authority.com");

        private Mock<ILogger> mockLogger;
        private Mock<ITokenProvider> mockBearerTokenProvider1 = new Mock<ITokenProvider>();
        private Mock<ITokenProvider> mockBearerTokenProvider2 = new Mock<ITokenProvider>();
        private Mock<ITokenProvidersFactory> mockBearerTokenProvidersFactory;
        private Mock<IAzureDevOpsSessionTokenFromBearerTokenProvider> mockVstsSessionTokenFromBearerTokenProvider;
        private Mock<IAuthUtil> mockAuthUtil;

        private VstsCredentialProvider vstsCredentialProvider;
        private IDisposable environmentLock;

        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();

            mockBearerTokenProvider1 = new Mock<ITokenProvider>();
            mockBearerTokenProvider1.Setup(x => x.CanGetToken(It.IsAny<TokenRequest>())).Returns(true);
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((AuthenticationResult)null);
            mockBearerTokenProvider2 = new Mock<ITokenProvider>();
            mockBearerTokenProvider2.Setup(x => x.CanGetToken(It.IsAny<TokenRequest>())).Returns(true);
            mockBearerTokenProvider2.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((AuthenticationResult)null);
            mockBearerTokenProvidersFactory = new Mock<ITokenProvidersFactory>();
            mockBearerTokenProvidersFactory.Setup(x => x.GetAsync(It.IsAny<Uri>())).ReturnsAsync(new[] { mockBearerTokenProvider1.Object, mockBearerTokenProvider2.Object });

            mockVstsSessionTokenFromBearerTokenProvider = new Mock<IAzureDevOpsSessionTokenFromBearerTokenProvider>();
            mockVstsSessionTokenFromBearerTokenProvider.Setup(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));

            mockAuthUtil = new Mock<IAuthUtil>();
            mockAuthUtil
                .Setup(x => x.GetAuthorizationInfoAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new AuthorizationInfo() { EntraAuthorityUri = testAuthority }));
            mockAuthUtil
                .Setup(x => x.GetAuthorizationEndpoint(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Uri("https://test.vssps.vsts.me"));

            vstsCredentialProvider = new VstsCredentialProvider(
                mockLogger.Object,
                mockAuthUtil.Object,
                mockBearerTokenProvidersFactory.Object,
                mockVstsSessionTokenFromBearerTokenProvider.Object);
            environmentLock = EnvironmentLock.WaitAsync().Result;
            ResetEnvVars();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ResetEnvVars();
            environmentLock?.Dispose();
        }

        private void ResetEnvVars()
        {
            Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, null);
            Environment.SetEnvironmentVariable(EnvUtil.EntraTokenOptInEnvVar, null);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsTrueForKnownSources()
        {
            var sources = new[]
            {
                @"https://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json",
                @"https://pkgs.codedev.ms/example/_packaging/TestFeed/nuget/v3/index.json",
                @"https://pkgs.codeapp.ms/example/_packaging/TestFeed/nuget/v3/index.json",
                @"https://example.pkgs.visualstudio.com/_packaging/TestFeed/nuget/v3/index.json",
                @"https://pkgs.dev.azure.com/example/_packaging/TestFeed/nuget/v3/index.json",
            };

            foreach (var source in sources)
            {
                var sourceUri = new Uri(source);
                var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(sourceUri);
                canProvideCredentials.Should().BeTrue($"because {source} is a known host");
            }

            mockAuthUtil
                .Verify(x => x.GetAzDevDeploymentType(It.IsAny<Uri>()), Times.Never, "because we shouldn't probe for known sources");
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsFalseForKnownHttpSource()
        {
            var source = new Uri("http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");

            var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(source);

            canProvideCredentials.Should().BeFalse();
            mockAuthUtil.Verify(x => x.GetAzDevDeploymentType(It.IsAny<Uri>()), Times.Never);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsTrueForOverriddenSources()
        {
            var sources = new[]
            {
                new Uri(@"https://example.overrideOne.com/_packaging/TestFeed/nuget/v3/index.json"),
                new Uri(@"https://example.overrideTwo.com/_packaging/TestFeed/nuget/v3/index.json"),
                new Uri(@"https://example.overrideThre.com/_packaging/TestFeed/nuget/v3/index.json"),
            };

            Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, string.Join(";", sources.Select(x => x.Host)));
            foreach (var source in sources)
            {
                var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(source);
                canProvideCredentials.Should().BeTrue($"because {source} is an overridden host");
            }

            mockAuthUtil
                .Verify(x => x.GetAzDevDeploymentType(It.IsAny<Uri>()), Times.Never, "because we shouldn't probe for known sources");

            Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, string.Empty);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsFalseForOverriddenHttpSource()
        {
            var source = new Uri("http://packages.example.com/_packaging/TestFeed/nuget/v3/index.json");
            Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, source.Host);

            var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(source);

            canProvideCredentials.Should().BeFalse();
            mockAuthUtil.Verify(x => x.GetAzDevDeploymentType(It.IsAny<Uri>()), Times.Never);
        }

        [TestMethod]
        public async Task CanProvideCredentials_ReturnsFalseForUnknownHttpHostsWithoutProbing()
        {
            var sources = new[]
            {
                new Uri(@"http://example.overrideOne.com/_packaging/TestFeed/nuget/v3/index.json"),
                new Uri(@"http://example.overrideTwo.com/_packaging/TestFeed/nuget/v3/index.json"),
            };

            foreach (var source in sources)
            {
                var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(source);
                canProvideCredentials.Should().BeFalse($"because {source} is not a valid host over HTTP");
            }

            mockAuthUtil
                .Verify(x => x.GetAzDevDeploymentType(It.IsAny<Uri>()), Times.Never, "because HTTP hosts should be rejected without probing");
        }

        [TestMethod]
        public async Task CanProvideCredentials_RejectsUnknownHttpsHostsAfterDiagnosticProbe()
        {
            var sources = new[]
            {
                new Uri(@"https://example.overrideOne.com/_packaging/TestFeed/nuget/v3/index.json"),
                new Uri(@"https://example.overrideTwo.com/_packaging/TestFeed/nuget/v3/index.json"),
            };

            foreach (var source in sources)
            {
                var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(source);
                canProvideCredentials.Should().BeFalse($"because {source} is not a valid host");
            }

            mockAuthUtil
                .Verify(x => x.GetAzDevDeploymentType(It.IsAny<Uri>()), Times.Exactly(2), "because unknown HTTPS hosts are probed only for diagnostics");
        }

        [TestMethod]
        public async Task CanProvideCredentials_RejectsUnknownHostEvenWhenProbeWouldReturnHosted()
        {
            mockAuthUtil.Setup(x => x.GetAzDevDeploymentType(It.IsAny<Uri>())).ReturnsAsync(AzDevDeploymentType.Hosted);

            var canProvideCredentials = await vstsCredentialProvider.CanProvideCredentialsAsync(
                new Uri("https://attacker.example.com/_packaging/TestFeed/nuget/v3/index.json"));

            canProvideCredentials.Should().BeFalse();
            mockAuthUtil.Verify(x => x.GetAzDevDeploymentType(It.IsAny<Uri>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleRequestAsync_DoesNotRunBearerTokenProviderWhenShouldRunFalse()
        {
            mockBearerTokenProvider1.Setup(x => x.CanGetToken(It.IsAny<TokenRequest>())).Returns(false);
            await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);
            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task HandleRequestAsync_DoesNotAcquireTokenForUntrustedCredentialDestination()
        {
            var requestUri = new Uri("https://attacker.example.com/_packaging/TestFeed/nuget/v3/index.json");
            mockBearerTokenProvider1.Setup(x => x.IsInteractive).Returns(true);

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(requestUri, false, false, false), CancellationToken.None);

            response.Should().BeNull();
            mockAuthUtil.Verify(x => x.GetAuthorizationEndpoint(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(
                x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
                Times.Never,
                "because an interactive Compact token must not be minted for an untrusted destination");
        }

        [TestMethod]
        public async Task HandleRequestAsync_DoesNotAcquireTokenForKnownHttpHost()
        {
            var requestUri = new Uri("http://example.pkgs.vsts.me/_packaging/TestFeed/nuget/v3/index.json");

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(requestUri, false, false, false), CancellationToken.None);

            response.Should().BeNull();
            mockAuthUtil.Verify(x => x.GetAuthorizationEndpoint(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(
                x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task HandleRequestAsync_DoesNotAcquireEntraTokenForUntrustedCredentialDestination()
        {
            var requestUri = new Uri("https://attacker.example.com/_packaging/TestFeed/nuget/v3/index.json");
            Environment.SetEnvironmentVariable(EnvUtil.EntraTokenOptInEnvVar, "true");

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(requestUri, false, false, false), CancellationToken.None);

            response.Should().BeNull();
            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task HandleRequestAsync_DoesNotAcquireEntraTokenWithoutAuthorizationEndpoint()
        {
            Environment.SetEnvironmentVariable(EnvUtil.EntraTokenOptInEnvVar, "true");
            mockAuthUtil
                .Setup(x => x.GetAuthorizationEndpoint(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Uri)null);

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);

            response.Should().BeNull();
            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task HandleRequestAsync_ReturnsEntraTokenToConfiguredCustomHost()
        {
            var requestUri = new Uri("https://packages.example.com/_packaging/TestFeed/nuget/v3/index.json");
            var token = GetToken("aadtoken");
            Environment.SetEnvironmentVariable(EnvUtil.SupportedHostsEnvVar, requestUri.Host);
            Environment.SetEnvironmentVariable(EnvUtil.EntraTokenOptInEnvVar, "true");
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(requestUri, false, false, false), CancellationToken.None);

            response.Username.Should().Be("EntraToken");
            response.Password.Should().Be(token.AccessToken);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(
                x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task HandleRequestAsync_RunsBearerTokenProviderWhenShouldRunTrue()
        {
            mockBearerTokenProvider1.Setup(x => x.CanGetToken(It.IsAny<TokenRequest>())).Returns(true);
            await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);
            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleRequestAsync_RunsNextBearerTokenProviderOnException()
        {
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("bad"));
            await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);
            mockBearerTokenProvider2.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleRequestAsync_RunsNextBearerTokenProviderOnReturnNull()
        {
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((AuthenticationResult)null);
            await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);
            mockBearerTokenProvider2.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleRequestAsync_ExchangesBearerTokenForSessionTokenAndReturnsToken()
        {
            var token = GetToken("aadtoken");
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);
            mockVstsSessionTokenFromBearerTokenProvider.Setup(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync("sessiontoken");

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);

            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mockBearerTokenProvider2.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            response.Password.Should().Be("sessiontoken");
        }

        [TestMethod]
        public async Task HandleRequestAsync_TriesNextBearerTokenProviderWhenExchangeBearerTokenForSessionTokenFails()
        {
            var token1 = GetToken("aadtoken1");
            var token2 = GetToken("aadtoken2");
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(token1);
            mockBearerTokenProvider2.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(token2);
            mockVstsSessionTokenFromBearerTokenProvider.Setup(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token1.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);
            mockVstsSessionTokenFromBearerTokenProvider.Setup(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token2.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync("sessiontoken");

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);

            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mockBearerTokenProvider2.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token1.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token2.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            response.Password.Should().Be("sessiontoken");
        }

        [TestMethod]
        public async Task HandleRequestAsync_ReturnsNullWhenAllBearerTokensBad()
        {
            var token1 = GetToken("aadtoken1");
            var token2 = GetToken("aadtoken2");
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(token1);
            mockBearerTokenProvider2.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(token2);
            mockVstsSessionTokenFromBearerTokenProvider.Setup(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token1.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);
            mockVstsSessionTokenFromBearerTokenProvider.Setup(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token2.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);

            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mockBearerTokenProvider2.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token1.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            mockVstsSessionTokenFromBearerTokenProvider.Verify(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token2.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            response.Should().BeNull();
        }

        [TestMethod]
        public async Task HandleRequestAsync_DoesNotTryNextProvider_WhenSpsEndpointIsUntrusted()
        {
            var token1 = GetToken("aadtoken1");
            mockBearerTokenProvider1.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(token1);
            mockVstsSessionTokenFromBearerTokenProvider
                .Setup(x => x.GetAzureDevOpsSessionTokenFromBearerToken(It.IsAny<GetAuthenticationCredentialsRequest>(), token1.AccessToken, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UntrustedSpsEndpointException("untrusted sps endpoint"));

            var response = await vstsCredentialProvider.HandleRequestAsync(new GetAuthenticationCredentialsRequest(testUri, false, false, false), CancellationToken.None);

            mockBearerTokenProvider1.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mockBearerTokenProvider2.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            response.Should().BeNull();
        }

        private AuthenticationResult GetToken(string token)
        {
            return new AuthenticationResult(token, false, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, null, null, null, Guid.Empty);
        }
    }
}
