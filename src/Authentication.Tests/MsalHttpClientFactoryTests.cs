// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Artifacts.Authentication.Tests;

[TestClass]
public class MsalHttpClientFactoryTests
{
    [TestMethod]
    public void GetHttpClientTest()
    {
        var httpClient = new HttpClient();
        var httpClientFactory = new MsalHttpClientFactory(httpClient);

        Assert.AreEqual(httpClient, httpClientFactory.GetHttpClient());
    }

    [TestMethod]
    public void UserAgentTest()
    {
        var httpClientFactory = new MsalHttpClientFactory(new HttpClient());
        var httpClient = httpClientFactory.GetHttpClient();
        var userAgent = httpClient.DefaultRequestHeaders.UserAgent;

        Assert.AreEqual(4, userAgent.Count);

        var programProduct = userAgent.ElementAt(0);
        Assert.IsNotNull(programProduct.Product);
        Assert.AreEqual(MsalHttpClientFactory.ProgramProduct, programProduct);
        Assert.AreEqual(PlatformInformation.GetProgramName(), programProduct.Product.Name);
        Assert.AreEqual(PlatformInformation.GetProgramVersion(), programProduct.Product.Version);

        var programComment = userAgent.ElementAt(1);
        Assert.AreEqual(MsalHttpClientFactory.ProgramComment, programComment);
        Assert.AreEqual($"({PlatformInformation.GetOSType()}; {PlatformInformation.GetCpuArchitecture()}; {PlatformInformation.GetOsDescription()})", programComment.Comment);

        var clrProduct = userAgent.ElementAt(2);
        Assert.IsNotNull(clrProduct.Product);
        Assert.AreEqual(MsalHttpClientFactory.ClrProduct, clrProduct);
        Assert.AreEqual("CLR", clrProduct.Product.Name);
        Assert.AreEqual(PlatformInformation.GetClrVersion(), clrProduct.Product.Version);

        var clrComment = userAgent.ElementAt(3);
        Assert.AreEqual(MsalHttpClientFactory.ClrComment, clrComment);
        Assert.AreEqual($"({PlatformInformation.GetClrFramework()}; {PlatformInformation.GetClrRuntime()}; {PlatformInformation.GetClrDescription()})", clrComment.Comment);
    }
}
