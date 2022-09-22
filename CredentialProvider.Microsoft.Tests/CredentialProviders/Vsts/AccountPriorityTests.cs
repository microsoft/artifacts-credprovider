// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetCredentialProvider.CredentialProviders.Vsts;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.Vsts
{
    [TestClass]
    public class AccountPriorityTests
    {
        private class TestAccount : IAccount
        {
            public string Username {get; set;}
            public string Environment {get; set;}
            public AccountId HomeAccountId {get; set;}
        }

        private static readonly Guid ContosoTenant = Guid.NewGuid();
        private static readonly Guid FabrikamTenant = Guid.NewGuid();
        
        private static readonly IAccount FabrikamUser = new TestAccount
        {
            Username = "billg@fabrikam.com",
            Environment = string.Empty,
            HomeAccountId = new AccountId(string.Empty, string.Empty, FabrikamTenant.ToString()),
        };

        private static readonly IAccount ContosoUser = new TestAccount
        {
            Username = "billg@contoso.com",
            Environment = string.Empty,
            HomeAccountId = new AccountId(string.Empty, string.Empty, ContosoTenant.ToString()),
        };

        private static readonly IAccount MsaUser = new TestAccount
        {
            Username = "bill.gates@live.com",
            Environment = string.Empty,
            HomeAccountId = new AccountId(string.Empty, string.Empty, AuthUtil.MsaAccountTenant.ToString()),
        };

        private static readonly List<List<IAccount>> Permutations = new List<List<IAccount>>() {
            new List<IAccount> { ContosoUser, MsaUser, FabrikamUser },
            new List<IAccount> { ContosoUser, FabrikamUser, MsaUser },
            new List<IAccount> { MsaUser, ContosoUser, FabrikamUser },
            new List<IAccount> { MsaUser, FabrikamUser, ContosoUser },
            new List<IAccount> { FabrikamUser, MsaUser, ContosoUser },
            new List<IAccount> { FabrikamUser, ContosoUser, MsaUser },
        };

        [TestMethod]
        public void MsaMatchesMsa()
        {
            foreach (var accounts in Permutations)
            {
                var sorted = MsalTokenProvider.PrioritizeAccounts(accounts, AuthUtil.MsaAuthorityTenant, null);
                Assert.AreEqual(sorted[0].Item1.Username, MsaUser.Username);
            }
        }

        [TestMethod]
        public void ContosoMatchesContoso()
        {
            foreach (var accounts in Permutations)
            {
                var sorted = MsalTokenProvider.PrioritizeAccounts(accounts, ContosoTenant, null);
                Assert.AreEqual(sorted[0].Item1.Username, ContosoUser.Username);
            }
        }

        [TestMethod]
        public void FabrikamMatchesFabrikam()
        {
            foreach (var accounts in Permutations)
            {
                var sorted = MsalTokenProvider.PrioritizeAccounts(accounts, FabrikamTenant, null);
                Assert.AreEqual(sorted[0].Item1.Username, FabrikamUser.Username);
            }
        }

        [TestMethod]
        public void LoginHintOverride()
        {
            foreach (var accounts in Permutations)
            {
                foreach (var tenantId in Permutations[0].Select(a => Guid.Parse(a.HomeAccountId.TenantId)))
                {
                    foreach (var loginHint in Permutations[0].Select(a => a.Username))
                    {
                        var sorted = MsalTokenProvider.PrioritizeAccounts(accounts, tenantId, loginHint);
                        Assert.AreEqual(sorted[0].Item1.Username, loginHint);
                    }
                }
            }
        }

        [TestMethod]
        public void UnknownAuthorityTenantPrefersMsa()
        {
            foreach (var accounts in Permutations)
            {
                var sorted = MsalTokenProvider.PrioritizeAccounts(accounts, null, null);
                Assert.AreEqual(sorted[0].Item1.Username, MsaUser.Username);
            }
        }
    }
}
