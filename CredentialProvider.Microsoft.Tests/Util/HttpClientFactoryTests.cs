// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Linq;
using Microsoft.Artifacts.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetCredentialProvider.Util;

namespace CredentialProvider.Microsoft.Tests.Util;

[TestClass]
public class HttpClientFactoryTests
{
    [TestMethod]
    public void HttpClientFactory_UserAgent()
    {
        var httpClientFactory = HttpClientFactory.Default;
        var httpClient = httpClientFactory.GetHttpClient();
        var userAgent = httpClient.DefaultRequestHeaders.UserAgent;

        Assert.AreEqual(4, userAgent.Count);
        Assert.AreEqual(MsalHttpClientFactory.ProgramProduct, userAgent.ElementAt(0));
        Assert.AreEqual(MsalHttpClientFactory.ProgramComment, userAgent.ElementAt(1));
        Assert.AreEqual(MsalHttpClientFactory.ClrProduct, userAgent.ElementAt(2));
        Assert.AreEqual(MsalHttpClientFactory.ClrComment, userAgent.ElementAt(3));
    }
}
