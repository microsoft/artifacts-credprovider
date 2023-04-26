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
        var tokenRequest = new TokenRequest(PackageUri);

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
        var tokenRequest = new TokenRequest(PackageUri);
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
        var tokenRequest = new TokenRequest(PackageUri);

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
    public void MsalDeviceCodeFlowContractTest()
    {
        var tokenProvider = new MsalDeviceCodeTokenProvider(appMock.Object, loggerMock.Object);
        var tokenRequest = new TokenRequest(PackageUri);

        Assert.IsNotNull(tokenProvider.Name);
        Assert.IsTrue(tokenProvider.IsInteractive);

        tokenRequest.IsInteractive = false;
        Assert.IsFalse(tokenProvider.CanGetToken(tokenRequest));

        tokenRequest.IsInteractive = true;
        Assert.IsTrue(tokenProvider.CanGetToken(tokenRequest));
    }
}
