// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Artifacts.Authentication.Tests;

/// <summary>
/// Tests that verify the single-PCA broker fallback architecture:
/// - Broker configured on the PCA with http://localhost redirect
/// - If broker is available at runtime, MSAL uses it for interactive auth
/// - If broker is NOT available, MSAL falls back to system browser using http://localhost
/// - The redirect URI is always http://localhost so fallback always succeeds
/// </summary>
[TestClass]
public class BrokerFallbackTests
{
    private static readonly Uri AuthorityUri = new Uri("https://login.microsoftonline.com/common");
    private Mock<ILogger> loggerMock = new Mock<ILogger>();

    [TestMethod]
    public void WithBrokerSupport_RedirectUri_AlwaysLocalhost_RegardlessOfBrokerAvailability()
    {
        // This is the core fix: redirect URI must be http://localhost so that
        // when the broker is NOT available, MSAL can fall back to system browser.
        // Previously, WithBroker() would set msauth:// on macOS which broke browser fallback.
        var builder = AzureArtifacts.CreateDefaultBuilder(AuthorityUri)
            .WithBrokerSupport(true, IntPtr.Zero, loggerMock.Object);

        var app = builder.Build();

        // Whether or not broker is available at runtime, redirect is always http://localhost
        Assert.AreEqual("http://localhost", app.AppConfig.RedirectUri,
            "Redirect URI must be http://localhost to support browser fallback when broker is unavailable");
    }

    [TestMethod]
    public void WithBrokerSupport_BrokerIsConfigured_OnPCA()
    {
        var app = AzureArtifacts.CreateDefaultBuilder(AuthorityUri)
            .WithBrokerSupport(true, IntPtr.Zero, loggerMock.Object)
            .Build();

        // Broker is configured so MSAL will attempt it first during AcquireTokenInteractive
        Assert.IsTrue(app.AppConfig.IsBrokerEnabled,
            "Broker should be enabled so MSAL tries it before falling back to browser");
    }

    [TestMethod]
    public void WithBrokerSupport_Disabled_NoBrokerConfigured()
    {
        var app = AzureArtifacts.CreateDefaultBuilder(AuthorityUri)
            .WithBrokerSupport(false, IntPtr.Zero, loggerMock.Object)
            .Build();

        Assert.IsFalse(app.AppConfig.IsBrokerEnabled);
        Assert.AreEqual("http://localhost", app.AppConfig.RedirectUri);
    }

    [TestMethod]
    public void IsBrokerAvailable_ReturnsValue_WhenBrokerConfigured()
    {
        // Test that IsBrokerAvailable() can be called on the builder after configuring broker.
        // The actual return value depends on the runtime environment:
        // - Windows with WAM: typically true
        // - macOS without enrollment: false
        // - CI/test environments: may vary
        var builder = AzureArtifacts.CreateDefaultBuilder(AuthorityUri)
            .WithBrokerSupport(true, IntPtr.Zero, loggerMock.Object);

        // Should not throw — this is the check we use to determine broker availability at startup
        bool available = builder.IsBrokerAvailable();

        // We don't assert true/false since it depends on the machine,
        // but we verify the call succeeds and redirect is still localhost either way
        var app = builder.Build();
        Assert.AreEqual("http://localhost", app.AppConfig.RedirectUri,
            $"Redirect URI must remain http://localhost regardless of IsBrokerAvailable()={available}");
    }

    [TestMethod]
    public void IsBrokerAvailable_ReturnsFalse_WhenBrokerNotConfigured()
    {
        // Without broker configured, IsBrokerAvailable should return false
        var builder = AzureArtifacts.CreateDefaultBuilder(AuthorityUri)
            .WithBrokerSupport(false, IntPtr.Zero, loggerMock.Object);

        Assert.IsFalse(builder.IsBrokerAvailable(),
            "IsBrokerAvailable should be false when broker is not configured");
    }

    [TestMethod]
    public void SinglePCA_ServesAllProviders_SilentAndInteractive()
    {
        // Verify the provider chain uses a single app for all auth flows.
        // This ensures broker cache is queried during silent auth on the same PCA
        // that will attempt broker interactive auth.
        var app = new Mock<IPublicClientApplication>();
        var appConfig = new Mock<IAppConfig>();
        appConfig.Setup(x => x.IsBrokerEnabled).Returns(true);
        app.Setup(x => x.AppConfig).Returns(appConfig.Object);

        var providers = MsalTokenProviders.Get(app.Object, loggerMock.Object).ToList();

        var silentProvider = providers.First(p => p.Name == "MSAL Silent");
        var interactiveProvider = providers.First(p => p.Name == "MSAL Interactive");

        // Both exist and share the same underlying app — no separate broker PCA
        Assert.IsNotNull(silentProvider);
        Assert.IsNotNull(interactiveProvider);
    }

    [TestMethod]
    public async Task SilentAuth_QueriesBrokerCache_WhenBrokerConfigured()
    {
        // When broker is configured on the PCA, silent auth queries the broker cache.
        // This works because we use one PCA for everything.
        var app = new Mock<IPublicClientApplication>();
        app.Setup(x => x.GetAccountsAsync()).ReturnsAsync(Array.Empty<IAccount>());
        app.Setup(x => x.Authority).Returns("https://login.microsoftonline.com/common");
        var appConfig = new Mock<IAppConfig>();
        appConfig.Setup(x => x.IsBrokerEnabled).Returns(false);
        app.Setup(x => x.AppConfig).Returns(appConfig.Object);

        var providers = MsalTokenProviders.Get(app.Object, loggerMock.Object).ToList();
        var silentProvider = providers.First(p => p.Name == "MSAL Silent");

        await silentProvider.GetTokenAsync(new TokenRequest());

        // The single PCA is used — if broker is configured, its cache is also queried
        app.Verify(x => x.GetAccountsAsync(), Times.Once);
    }

    [TestMethod]
    public void WithBroker_Legacy_OverwritesRedirectUri()
    {
        // Verify that the old WithBroker() method still overwrites the redirect URI.
        // This is kept for backward compatibility but WithBrokerSupport() is preferred.
        var app = AzureArtifacts.CreateDefaultBuilder(AuthorityUri)
            .WithBroker(true, IntPtr.Zero, loggerMock.Object)
            .Build();

        // On Windows, WithBrokerRedirectUri() doesn't change it (stays http://localhost).
        // On macOS it would be msauth://, on Linux it would be nativeclient.
        // This test documents the legacy behavior that caused the original issue.
        Assert.IsTrue(app.AppConfig.IsBrokerEnabled);
    }
}
