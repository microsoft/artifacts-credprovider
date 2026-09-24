// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    internal static class VstsEndpointPolicy
    {
        private static readonly string[] AllowedFeedHosts = new[]
        {
            ".pkgs.vsts.me",
            "pkgs.codedev.ms",
            "pkgs.codeapp.ms",
            ".pkgs.visualstudio.com",
            "pkgs.dev.azure.com",
        };

        private static readonly string[] AllowedSpsHosts = new[]
        {
            "vssps.visualstudio.com",              // Azure DevOps production
            ".vssps.visualstudio.com",             // Azure DevOps production (suffix)
            "vssps.dev.azure.com",                 // Azure DevOps production
            ".vssps.dev.azure.com",                // Azure DevOps production (suffix)
            "vsspsext.visualstudio.com",            // Extended SPS services
            "vsspsext.dev.azure.com",               // Extended SPS services
            "vssps.devppe.azure.com",              // PPE environment
            ".vssps.devppe.azure.com",             // PPE environment (suffix)
            "vssps.vsallin.net",                   // PPE/staging
            ".vssps.vsallin.net",                  // PPE/staging (suffix)
            ".vssps.codeapp.ms",                   // AppFabric
            ".vssps.vsts.io",                      // AppFabric API
            "vssps.codedev.ms",                    // DevFabric
            ".vssps.codedev.ms",                   // DevFabric
            ".vssps.vsts.me",                      // DevFabric
        };

        public static bool IsTrustedFeedEndpoint(Uri endpoint, IEnumerable<string> additionalHosts = null)
        {
            var allowedHosts = additionalHosts == null
                ? AllowedFeedHosts
                : AllowedFeedHosts.Concat(additionalHosts);

            return endpoint.IsHttpsHostAllowed(allowedHosts);
        }

        public static bool IsTrustedSpsEndpoint(Uri endpoint)
        {
            return endpoint.IsHttpsHostAllowed(AllowedSpsHosts);
        }
    }
}