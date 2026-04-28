// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetCredentialProvider.CredentialProviders.Vsts;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts
{
    [TestClass]
    public class VstsSessionTokenClientTests
    {
        [TestMethod]
        [DataRow("https://app.vssps.visualstudio.com")]
        [DataRow("https://app.vssps.visualstudio.com/")]
        [DataRow("https://APP.VSSPS.VISUALSTUDIO.COM")]
        [DataRow("https://wcus0.app.vssps.visualstudio.com")]
        [DataRow("https://vssps.dev.azure.com")]
        [DataRow("https://app.vssps.dev.azure.com")]
        [DataRow("https://wcus0.app.vssps.dev.azure.com")]
        [DataRow("https://org.vssps.visualstudio.com")]
        [DataRow("https://test.vssps.codeapp.ms")]
        [DataRow("https://test.vssps.codedev.ms")]
        [DataRow("https://test.vssps.vsts.me")]
        public void IsAllowedSpsEndpoint_KnownHosts_ReturnsTrue(string url)
        {
            VstsSessionTokenClient.IsAllowedSpsEndpoint(new Uri(url)).Should().BeTrue(
                $"because {url} is a known Azure DevOps SPS host");
        }

        [TestMethod]
        [DataRow("https://evil.example.com")]
        [DataRow("https://attacker.com/capture")]
        [DataRow("https://vssps.visualstudio.com.evil.com")]
        [DataRow("https://notvssps.visualstudio.com")]
        [DataRow("http://app.vssps.visualstudio.com")]
        [DataRow("https://evil.com")]
        [DataRow("https://login.microsoftonline.com")]
        [DataRow("https://dev.azure.com")]
        [DataRow("https://pkgs.dev.azure.com")]
        public void IsAllowedSpsEndpoint_UnknownHosts_ReturnsFalse(string url)
        {
            VstsSessionTokenClient.IsAllowedSpsEndpoint(new Uri(url)).Should().BeFalse(
                $"because {url} is NOT a known Azure DevOps SPS host");
        }

        [TestMethod]
        public void IsAllowedSpsEndpoint_HttpScheme_ReturnsFalse()
        {
            VstsSessionTokenClient.IsAllowedSpsEndpoint(
                new Uri("http://app.vssps.visualstudio.com")).Should().BeFalse(
                "because plain HTTP should never be accepted for bearer token exchange");
        }
    }
}
