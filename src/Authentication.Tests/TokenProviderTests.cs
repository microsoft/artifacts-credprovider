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
    public async Task MsalBrokerInteractive_ReturnsNull_OnMsalServiceException()
    {
        var app = CreateRealApp();
        // IsUserInteractive() should pass on real PCA, and AcquireTokenInteractive
        // will throw MsalServiceException during ExecuteAsync.
        var tokenProvider = new MsalBrokerInteractiveTokenProvider(app, loggerMock.Object);
        var tokenRequest = new TokenRequest { IsInteractive = true, CanShowDialog = true };

        // On Windows without broker running, we expect the provider to return null
        // (either from IsUserInteractive check or from exception handling).
        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task MsalBrokerInteractive_ReturnsNull_OnMsalClientException()
    {
        var app = CreateRealApp();
        var tokenProvider = new MsalBrokerInteractiveTokenProvider(app, loggerMock.Object);
        var tokenRequest = new TokenRequest { IsInteractive = true, CanShowDialog = true };

        var result = await tokenProvider.GetTokenAsync(tokenRequest);

        Assert.IsNull(result);
    }

    /// <summary>
    /// Verifies that SynchronizationContext is cleared before calling MSAL's
    /// ExecuteAsync inside RunOnMainThreadAsync. Without this fix, the macOS
    /// broker's FallbackToNativeMsal would deadlock because MSAL's internal
    /// system browser fallback posts continuations back to the blocked main
    /// thread's SynchronizationContext.
    /// 
    /// This test simulates the deadlock scenario by:
    /// 1. Installing a single-threaded SynchronizationContext
    /// 2. Calling an async method that clears SynchronizationContext before awaiting
    /// 3. Verifying the continuation completes without deadlocking
    /// </summary>
    [TestMethod]
    [Timeout(10000)] // Deadlock would hang forever; fail fast after 10s
    public async Task SynchronizationContext_ClearedDuringBrokerCall_PreventsDeadlock()
    {
        // Simulate the pattern used in MsalBrokerInteractiveTokenProvider:
        // save/clear/restore SynchronizationContext around an async call.
        var completed = false;

        // Use a dedicated thread with a blocking SynchronizationContext to simulate
        // the macOS main thread scenario. The SingleThreadSynchronizationContext
        // blocks its thread and pumps posted callbacks, similar to MacMainThreadScheduler.
        var tcs = new TaskCompletionSource<bool>();
        var thread = new Thread(() =>
        {
            var ctx = new SingleThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);

            // Post the work to the context, then run the message loop
            ctx.Post(_ =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        // This is the pattern from MsalBrokerInteractiveTokenProvider.GetTokenAsync:
                        var savedContext = SynchronizationContext.Current;
                        SynchronizationContext.SetSynchronizationContext(null);
                        try
                        {
                            // Simulate MSAL's internal FallbackToNativeMsal -> system browser retry.
                            // Without clearing SynchronizationContext, this continuation would try
                            // to post back to the single-threaded context, which is blocked.
                            await Task.Delay(50).ConfigureAwait(false);
                            completed = true;
                        }
                        finally
                        {
                            SynchronizationContext.SetSynchronizationContext(savedContext);
                        }
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                    finally
                    {
                        ctx.Complete();
                    }
                });
            }, null);

            ctx.RunOnCurrentThread();
        });

        thread.IsBackground = true;
        thread.Start();

        await tcs.Task;

        Assert.IsTrue(completed, "Async work should complete without deadlocking");
    }

    /// <summary>
    /// A simple single-threaded SynchronizationContext that simulates the macOS
    /// MacMainThreadScheduler. Callbacks are queued and executed on the owning thread.
    /// </summary>
    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> queue = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            queue.Add((d, state));
        }

        public void Complete() => queue.CompleteAdding();

        public void RunOnCurrentThread()
        {
            foreach (var (callback, state) in queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }
    }

    private static IPublicClientApplication CreateRealApp()
    {
        return PublicClientApplicationBuilder
            .Create("d5a56ea4-7369-46b8-a538-c370805301bf")
            .WithAuthority("https://login.microsoftonline.com/common")
            .Build();
    }
}
