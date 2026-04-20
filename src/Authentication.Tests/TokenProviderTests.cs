// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Artifacts.Authentication.Tests;

[TestClass]
public class TokenProviderTests
{
    private static readonly Uri PackageUri = new Uri("https://pkgs.dev.azure.com/mseng/PipelineTools/_packaging/artifacts-credprovider/nuget/v3/index.json");

    private Mock<IPublicClientApplication> appMock = new Mock<IPublicClientApplication>(MockBehavior.Strict);
    private Mock<ILogger> loggerMock = new Mock<ILogger>();

    [TestMethod]
    public void MsalSilentContractTest()
    {
        var tokenProvider = new MsalSilentTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest();

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsFalse(tokenProvider.IsInteractive);
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsRetry = true;
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));
    }

    [TestMethod]
    public void MsalIntegratedWindowsAuthContractTest()
    {
        var tokenProvider = new MsalIntegratedWindowsAuthTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest();
        var windowsIntegratedAuthSupported = Environment.OSVersion.Platform == PlatformID.Win32NT;

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsFalse(tokenProvider.IsInteractive);

        tokenRequest.IsWindowsIntegratedAuthEnabled = false;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsWindowsIntegratedAuthEnabled = true;
        Assert.AreEqual(windowsIntegratedAuthSupported, tokenProvider.CanGetToken(tokenRequest));
    }

    [TestMethod]
    public void MsalInteractiveContractTest()
    {
        var tokenProvider = new MsalInteractiveTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest();

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsTrue(tokenProvider.IsInteractive);

        tokenRequest.IsInteractive = false;
        tokenRequest.CanShowDialog = false;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = false;
        tokenRequest.CanShowDialog = true;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = true;
        tokenRequest.CanShowDialog = false;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = true;
        tokenRequest.CanShowDialog = true;
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));

        // non-interactive variants for those who don't not avoid framework guidelines
        tokenRequest.IsNonInteractive = false;
        tokenRequest.CanShowDialog = true;
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsNonInteractive = true;
        tokenRequest.CanShowDialog = true;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));
    }

    [TestMethod]
    public void MsalBrokerInteractiveContractTest()
    {
        var tokenProvider = new MsalBrokerInteractiveTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest();

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsTrue(tokenProvider.IsInteractive);

        tokenRequest.IsInteractive = false;
        tokenRequest.CanShowDialog = false;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = false;
        tokenRequest.CanShowDialog = true;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = true;
        tokenRequest.CanShowDialog = false;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = true;
        tokenRequest.CanShowDialog = true;
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));
    }

    [TestMethod]
    public void MsalDeviceCodeFlowContractTest()
    {
        var tokenProvider = new MsalDeviceCodeTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest();

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsTrue(tokenProvider.IsInteractive);

        tokenRequest.IsInteractive = false;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = true;
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));
    }

    [TestMethod]
    public void MsalServicePrincipalContractTest()
    {
        appMock.Setup(x => x.AppConfig)
            .Returns(new Mock<IAppConfig>().Object);

        var tokenProvider = new MsalServicePrincipalTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest();

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsFalse(tokenProvider.IsInteractive);

        tokenRequest.IsInteractive = true;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = false;
        tokenRequest.ClientId = "clientId";
        tokenRequest.ClientCertificate = Mock.Of<X509Certificate2>();
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = false;
        tokenRequest.ClientId = null;
        tokenRequest.ClientCertificate = Mock.Of<X509Certificate2>();
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = false;
        tokenRequest.ClientId = "clientId";
        tokenRequest.ClientCertificate = null;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));
    }


    [TestMethod]
    public void MsalManagedIdentityContractTest()
    {
        appMock.Setup(x => x.AppConfig)
            .Returns(new Mock<IAppConfig>().Object);

        var tokenProvider = new MsalManagedIdentityTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest();

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsFalse(tokenProvider.IsInteractive);

        tokenRequest.IsInteractive = true;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = false;
        tokenRequest.ClientId = "clientId";
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = false;
        tokenRequest.ClientId = null;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));
    }

    [TestMethod]
    public async Task MsalTokenProviders_SilentProvider_UsesBrokerApp_WhenProvided()
    {
        // Arrange: create two distinct app mocks (Loose so other providers in the iterator don't fail)
        var nonBrokerApp = new Mock<IPublicClientApplication>();
        var brokerApp = new Mock<IPublicClientApplication>();

        // broker app should be the one called for silent auth
        brokerApp.Setup(x => x.GetAccountsAsync())
            .ReturnsAsync(Array.Empty<IAccount>());
        brokerApp.Setup(x => x.Authority)
            .Returns("https://login.microsoftonline.com/common");
        var brokerAppConfig = new Mock<IAppConfig>();
        brokerAppConfig.Setup(x => x.IsBrokerEnabled).Returns(false);
        brokerApp.Setup(x => x.AppConfig).Returns(brokerAppConfig.Object);

        // non-broker app should NOT be called for silent auth
        nonBrokerApp.Setup(x => x.GetAccountsAsync())
            .ReturnsAsync(Array.Empty<IAccount>());

        var providers = MsalTokenProviders.Get(nonBrokerApp.Object, loggerMock.Object, appInteractiveBroker: brokerApp.Object).ToList();
        var silentProvider = providers.First(p => p.Name == "MSAL Silent");

        // Act
        await silentProvider.GetTokenAsync(new TokenRequest());

        // Assert: broker app was used, not the non-broker app
        brokerApp.Verify(x => x.GetAccountsAsync(), Times.Once);
        nonBrokerApp.Verify(x => x.GetAccountsAsync(), Times.Never);
    }

    [TestMethod]
    public async Task MsalTokenProviders_SilentProvider_UsesNonBrokerApp_WhenNoBrokerProvided()
    {
        // Arrange (Loose so other providers in the iterator don't fail)
        var nonBrokerApp = new Mock<IPublicClientApplication>();

        nonBrokerApp.Setup(x => x.GetAccountsAsync())
            .ReturnsAsync(Array.Empty<IAccount>());
        nonBrokerApp.Setup(x => x.Authority)
            .Returns("https://login.microsoftonline.com/common");
        var nonBrokerAppConfig = new Mock<IAppConfig>();
        nonBrokerAppConfig.Setup(x => x.IsBrokerEnabled).Returns(false);
        nonBrokerApp.Setup(x => x.AppConfig).Returns(nonBrokerAppConfig.Object);

        var providers = MsalTokenProviders.Get(nonBrokerApp.Object, loggerMock.Object).ToList();
        var silentProvider = providers.First(p => p.Name == "MSAL Silent");

        // Act
        await silentProvider.GetTokenAsync(new TokenRequest());

        // Assert: non-broker app was used
        nonBrokerApp.Verify(x => x.GetAccountsAsync(), Times.Once);
    }
}
