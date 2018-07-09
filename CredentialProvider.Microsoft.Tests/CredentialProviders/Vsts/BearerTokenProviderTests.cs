// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Logging;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts
{
    [TestClass]
    public class AdalVssCredentialProviderTests
    {
        private readonly CancellationToken cancellationToken = default(CancellationToken);
        private readonly Uri testAuthority = new Uri("https://example.aad.authority.com");

        private Mock<ILogger> mockLogger;
        private Mock<IAdalTokenProviderFactory> mockAdalTokenProviderFactory;
        private Mock<IAdalTokenProvider> mockAdalTokenProvider;
        private Mock<IAuthUtil> mockAuthUtil;

        private BearerTokenProvider bearerTokenProvider;

        [TestInitialize]
        public void TestInitialize()
        {
            mockLogger = new Mock<ILogger>();

            mockAdalTokenProvider = new Mock<IAdalTokenProvider>();

            mockAdalTokenProviderFactory = new Mock<IAdalTokenProviderFactory>();
            mockAdalTokenProviderFactory
                .Setup(x => x.Get(It.IsAny<string>()))
                .Returns(mockAdalTokenProvider.Object);

            mockAuthUtil = new Mock<IAuthUtil>();
            mockAuthUtil
                .Setup(x => x.GetAadAuthorityUriAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(testAuthority));

            bearerTokenProvider = new BearerTokenProvider(mockLogger.Object, mockAdalTokenProviderFactory.Object, mockAuthUtil.Object);
        }

        [TestMethod]
        public async Task Get_WithoutCachedToken_CallsUIFlow()
        {
            var source = new Uri("https://example.com/index.json");
            var isRetry = false;
            var isNonInteractive = false;
            var canShowDialog = true;

            var adalToken = "TestADALToken";
            MockCachedToken(null);
            MockUIToken(adalToken);

            var bearerToken = await bearerTokenProvider.GetAsync(source, isRetry, isNonInteractive, canShowDialog, cancellationToken);
            bearerToken.Should().Be(adalToken);

            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()), Times.Never);

            VerifyAuthority(source);
        }

        [TestMethod]
        public async Task Get_WithoutCachedTokenAndUIFlowCanceled_CallsDeviceCodeFlow()
        {
            var source = new Uri("https://example.com/index.json");
            var isRetry = false;
            var isNonInteractive = false;
            var canShowDialog = true;

            var adalToken = "TestADALToken";
            MockCachedToken(null);
            MockUIToken(null);
            MockDeviceFlowToken(adalToken);

            var bearerToken = await bearerTokenProvider.GetAsync(source, isRetry, isNonInteractive, canShowDialog, cancellationToken);
            bearerToken.Should().Be(adalToken);

            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()), Times.Once);

            VerifyAuthority(source);
        }

        [TestMethod]
        public async Task Get_WithCachedToken_DoesNotCallFlows()
        {
            var source = new Uri("https://example.com/index.json");
            var isRetry = false;
            var isNonInteractive = false;
            var canShowDialog = false;

            var adalToken = "TestADALToken";
            MockCachedToken(adalToken);

            var bearerToken = await bearerTokenProvider.GetAsync(source, isRetry, isNonInteractive, canShowDialog, cancellationToken);
            bearerToken.Should().Be(adalToken);

            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()), Times.Never);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()), Times.Never);

            VerifyAuthority(source);
        }

        [TestMethod]
        public async Task Get_IsRetry_DoesNotQueryCache()
        {
            var source = new Uri("https://example.com/index.json");
            var isRetry = true;
            var isNonInteractive = false;
            var canShowDialog = true;

            var adalToken = "TestADALToken";
            MockCachedToken("OldCachedToken");
            MockUIToken(adalToken);

            var bearerToken = await bearerTokenProvider.GetAsync(source, isRetry, isNonInteractive, canShowDialog, cancellationToken);
            bearerToken.Should().Be(adalToken);

            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()), Times.Never);

            VerifyAuthority(source);
        }

        [TestMethod]
        public async Task Get_WithoutCachedTokenAndIsNonInteractive_DoesNotCallFlows()
        {
            var source = new Uri("https://example.com/index.json");
            var isRetry = false;
            var isNonInteractive = true;
            var canShowDialog = false;

            MockCachedToken(null);

            var bearerToken = await bearerTokenProvider.GetAsync(source, isRetry, isNonInteractive, canShowDialog, cancellationToken);
            bearerToken.Should().BeNull();

            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()), Times.Never);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()), Times.Never);

            VerifyAuthority(source);
        }

        [TestMethod]
        public async Task Get_WithCachedTokenAndIsNonInteractive_DoesNotCallFlows()
        {
            var source = new Uri("https://example.com/index.json");
            var isRetry = false;
            var canShowDialog = false;
            var isNonInteractive = true;

            var adalToken = "TestADALToken";
            MockCachedToken(adalToken);

            var bearerToken = await bearerTokenProvider.GetAsync(source, isRetry, isNonInteractive, canShowDialog, cancellationToken);
            bearerToken.Should().Be(adalToken);

            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()), Times.Never);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()), Times.Never);

            VerifyAuthority(source);
        }

        [TestMethod]
        public async Task Get_IsRetryIsNonInteractive_ShouldWarn()
        {
            var source = new Uri("https://example.com/index.json");
            var isRetry = true;
            var isNonInteractive = true;
            var canShowDialog = false;

            var bearerToken = await bearerTokenProvider.GetAsync(source, isRetry, isNonInteractive, canShowDialog, cancellationToken);
            bearerToken.Should().BeNull();

            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()), Times.Never);
            mockAdalTokenProvider
                .Verify(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()), Times.Never);

            mockLogger
                .Verify(x => x.Log(NuGet.Common.LogLevel.Warning, It.IsAny<string>()));

            VerifyAuthority(source);
        }

        private void MockCachedToken(string token)
        {
            mockAdalTokenProvider
                .Setup(x => x.AcquireTokenSilentlyAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IAdalToken>(new AdalToken("Bearer", token)));
        }

        private void MockDeviceFlowToken(string token)
        {
            mockAdalTokenProvider
                .Setup(x => x.AcquireTokenWithDeviceFlowAsync(It.IsAny<Func<DeviceCodeResult, Task>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IAdalToken>(new AdalToken("Bearer", token)));
        }

        private void MockUIToken(string token)
        {
            mockAdalTokenProvider
                .Setup(x => x.AcquireTokenWithUI(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IAdalToken>(new AdalToken("Bearer", token)));
        }

        private void VerifyAuthority(Uri uri)
        {
            // Verify we're getting the correct authority for the request
            mockAuthUtil
                .Verify(x => x.GetAadAuthorityUriAsync(uri, It.IsAny<CancellationToken>()));

            // Verify we're getting the correct adal token provider for the request
            mockAdalTokenProviderFactory
                .Verify(x => x.Get(testAuthority.ToString()));
        }
    }
}
