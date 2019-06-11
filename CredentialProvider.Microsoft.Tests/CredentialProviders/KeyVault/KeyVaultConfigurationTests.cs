using Microsoft.Azure.KeyVault.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetCredentialProvider.CredentialProviders.KeyVault;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CredentialProvider.Microsoft.Tests.CredentialProviders.KeyVault
{
    [TestClass]
    public class KeyVaultConfigurationTests
    {
        [TestMethod]
        public void EnvironmentVariableConfigTest()
        {
            Environment.SetEnvironmentVariable(EnvUtil.KeyVaultUrlEnvVar, "https://TestKeyVault.vault.azure.net");
            var keyVaultProvider = new VstsKeyVaultCredentialProvider(new StandardOutputLogger());
            Assert.IsTrue(KeyVaultHelper.IsConfigured(out string keyVaultUrl));
            Assert.AreEqual("https://TestKeyVault.vault.azure.net", keyVaultUrl);
        }

        [TestMethod]
        public void SetConfigTest()
        {
            KeyVaultHelper.Config config = new KeyVaultHelper.Config()
            {
                KeyVaultUrl = "https://SetConfigTest.vault.azure.com",
                ClientId = "01f61b0e-8e36-4247-857c-4c3a0376bfae",
                CertificateStoreType = "CurrentUser",
                CertificateThumbprint = "a74eba1fa5bb7731c110178a0a821fc30ceb4ffa",
                UseMsi = false
            };

            KeyVaultHelper.Configure(config);
            Assert.IsTrue(KeyVaultHelper.IsConfigured(out string keyVaultUrl));
            Assert.AreEqual("https://SetConfigTest.vault.azure.com", keyVaultUrl);

            ConfigManager manager = new ConfigManager();
            string urlFromSettingsFile = manager.GetSetting("KeyVaultUrl");
            Assert.AreEqual("https://SetConfigTest.vault.azure.com", urlFromSettingsFile);

            string useMsiFromSettingsFile = manager.GetSetting("KeyVaultUseMsi");
            Assert.AreEqual("False", useMsiFromSettingsFile);

            string clientIdFromSettingsFile = manager.GetSetting("KeyVaultAuthClientId");
            Assert.AreEqual("01f61b0e-8e36-4247-857c-4c3a0376bfae", clientIdFromSettingsFile);

            string certificateStoreType = manager.GetSetting("KeyVaultAuthCertificateStoreType");
            Assert.AreEqual("CurrentUser", certificateStoreType);

            string certificateThumbprint = manager.GetSetting("KeyVaultAuthCertificateThumbprint");
            Assert.AreEqual("a74eba1fa5bb7731c110178a0a821fc30ceb4ffa", certificateThumbprint);
        }

        [TestMethod]
        public void KeyVaultProviderDefaultsTest()
        {
            Environment.SetEnvironmentVariable(EnvUtil.KeyVaultUrlEnvVar, "https://TestKeyVault.vault.azure.net");
            var keyVaultProvider = new VstsKeyVaultCredentialProvider(new StandardOutputLogger());
            bool result = keyVaultProvider.CanProvideCredentialsAsync(new Uri("https://contoso.com")).Result;
            Assert.IsTrue(result);
        }
    }
}
