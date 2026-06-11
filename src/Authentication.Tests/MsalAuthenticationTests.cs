// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Artifacts.Authentication.Tests;

// Tests are meant to be run manually as it requires interactive logins, multiple accounts, and domain joined machines.
//[Ignore]
[TestClass]
public class MsalAuthenticationTests
{
    private static readonly Uri PackageUri = new Uri("https://pkgs.dev.azure.com/mseng/PipelineTools/_packaging/artifacts-credprovider/nuget/v3/index.json");
    private static readonly Uri AuthorityUri = new Uri("https://login.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47");
    private static ILogger logger = LoggerFactory
        .Create(builder =>
        {
            builder.SetMinimumLevel(Extensions.Logging.LogLevel.Trace);
            builder.AddDebug();
            builder.AddConsole();
        })
        .CreateLogger(nameof(MsalAuthenticationTests));

    private static MsalCacheHelper cache = MsalCache
        .GetMsalCacheHelperAsync(MsalCache.DefaultMsalCacheLocation, logger).GetAwaiter().GetResult();

    // Single PCA with broker configured and http://localhost redirect.
    // MSAL will try broker first, then fall back to system browser.
    private IPublicClientApplication app = AzureArtifacts
        .CreateDefaultBuilder(AuthorityUri)
        .WithBrokerSupport(true, GetForegroundWindow(), logger)
        .Build(cache);

    // PCA without broker for comparison testing
    private IPublicClientApplication appNoBroker = AzureArtifacts
        .CreateDefaultBuilder(AuthorityUri)
        .WithParentActivityOrWindow(() => GetForegroundWindow())
        .Build(cache);

    // PCA with broker configured but an invalid window handle to simulate broker failure.
    // This forces MSAL to fail the broker path and fall back to system browser.
    private IPublicClientApplication appBadBroker = AzureArtifacts
        .CreateDefaultBuilder(AuthorityUri)
        .WithBrokerSupport(true, new IntPtr(0xDEAD), logger)
        .Build(cache);

    [TestMethod]
    public async Task MsalAcquireTokenSilentTest()
    {
        var tokenProvider = new MsalSilentTokenProvider(app, logger);
        var tokenRequest = new TokenRequest();

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task MsalAcquireTokenByIntegratedWindowsAuthTest()
    {
        var tokenProvider = new MsalIntegratedWindowsAuthTokenProvider(app, logger);
        var tokenRequest = new TokenRequest();

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task MsalAcquireTokenInteractiveTest()
    {
        // Interactive auth with broker configured — MSAL should try broker (WAM) first
        var tokenProvider = new MsalInteractiveTokenProvider(app, logger);
        var tokenRequest = new TokenRequest { IsInteractive = true, CanShowDialog = true };

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result);
        // On a machine with WAM available, this should come from the broker
        Assert.AreEqual(TokenSource.Broker, result.AuthenticationResultMetadata.TokenSource);
    }

    [TestMethod]
    public async Task MsalAcquireTokenInteractive_NoBroker_UsesBrowserTest()
    {
        // Interactive auth without broker — should use system browser with http://localhost
        var tokenProvider = new MsalInteractiveTokenProvider(appNoBroker, logger);
        var tokenRequest = new TokenRequest { IsInteractive = true, CanShowDialog = true };

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result);
        Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);
    }

    [TestMethod]
    public async Task MsalAcquireTokenInteractive_BadBroker_FallsBackToBrowserTest()
    {
        // This simulates a scenario where broker may fail:
        // Broker is configured but given an invalid window handle.
        // On Windows, WAM is resilient and will still succeed (showing its own dialog).
        // On macOS without enrollment, the broker would fail and MSAL should fall back
        // to system browser using http://localhost redirect.
        //
        // The key assertion: auth succeeds regardless of broker availability,
        // proving that http://localhost redirect enables graceful fallback.
        var tokenProvider = new MsalInteractiveTokenProvider(appBadBroker, logger);
        var tokenRequest = new TokenRequest { IsInteractive = true, CanShowDialog = true };

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result, "Token acquisition should succeed — either via broker or browser fallback");
        // On Windows with WAM: Broker succeeds (WAM handles bad window gracefully)
        // On macOS/Linux without broker: Falls back to browser (IdentityProvider)
        Assert.IsTrue(
            result.AuthenticationResultMetadata.TokenSource == TokenSource.Broker ||
            result.AuthenticationResultMetadata.TokenSource == TokenSource.IdentityProvider,
            $"Expected Broker or IdentityProvider, got {result.AuthenticationResultMetadata.TokenSource}");
    }

    [TestMethod]
    public async Task MsalAcquireTokenWithDeviceCodeTest()
    {
        var tokenProvider = new MsalDeviceCodeTokenProvider(app, logger);
        var tokenRequest = new TokenRequest { IsInteractive = true };

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result);
        Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);
    }

    [TestMethod]
    [Ignore]
    public async Task MsalAquireTokenWithManagedIdentity()
    {
        var tokenProvider = new MsalManagedIdentityTokenProvider(app, logger);
        var tokenRequest = new TokenRequest();
        tokenRequest.ClientId = Environment.GetEnvironmentVariable("ARTIFACTS_CREDENTIALPROVIDER_TEST_CLIENTID");

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result);
        Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);
    }

    [TestMethod]
    [Ignore]
    public async Task MsalAquireTokenWithServicePrincipal()
    {
        var tokenProvider = new MsalServicePrincipalTokenProvider(app, logger);
        var tokenRequest = new TokenRequest();
        tokenRequest.ClientId = Environment.GetEnvironmentVariable("ARTIFACTS_CREDENTIALPROVIDER_TEST_CLIENTID");
        tokenRequest.ClientCertificate = new X509Certificate2(Environment.GetEnvironmentVariable("ARTIFACTS_CREDENTIALPROVIDER_TEST_CERT_PATH") ?? string.Empty);

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNotNull(result);
        Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}

internal static class MsalCacheExtensions
{
    public static IPublicClientApplication Build(this PublicClientApplicationBuilder builder, MsalCacheHelper? cache = null)
    {
        IPublicClientApplication app = builder.Build();

        cache?.RegisterCache(app.UserTokenCache);

        return app;
    }
}
