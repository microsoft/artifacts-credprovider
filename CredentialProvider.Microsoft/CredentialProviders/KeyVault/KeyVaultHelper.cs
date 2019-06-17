// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Resources = NuGetCredentialProvider.Resources;

namespace Microsoft.Azure.KeyVault.Helper
{
    /// <summary>
    /// This class provides access to secrets stored in the KeyVault
    /// </summary>
    public sealed class KeyVaultHelper : IDisposable
    {
        public const string KeyVaultUrlSettingName = "KeyVaultUrl";
        public const string CertificateThumbprintSettingName = "KeyVaultAuthCertificateThumbprint";
        public const string CertificateStoreTypeSettingName = "KeyVaultAuthCertificateStoreType";
        public const string ClientIdSettingName = "KeyVaultAuthClientId";

        public struct Config
        {
            public string CertificateThumbprint { get; set; }
            public string CertificateStoreType { get; set; }
            public string KeyVaultUrl { get; set; }
            public string ClientId { get; set; }
            public bool? UseManagedServiceIdentity { get; set; }
        }

        private static KeyVaultHelper _instance = null;
        private static KeyVaultHelper.Config _config;

        private readonly string _keyVaultUrl;
        private readonly string _clientId;
        private readonly string _certificateThumbprint;
        private readonly StoreLocation _storeLocation;
        private readonly bool? _useMsi;
        private readonly KeyVaultClient _keyVaultClient;
        private string _accessToken;
        private DateTimeOffset _expiration;

        private KeyVaultHelper()
        {
            ConfigManager configManager = new ConfigManager();

            _keyVaultUrl = _config.KeyVaultUrl;
            if (string.IsNullOrWhiteSpace(_keyVaultUrl))
            {
                _keyVaultUrl = configManager.GetSetting(KeyVaultUrlSettingName);
                if (string.IsNullOrWhiteSpace(_keyVaultUrl))
                    throw new KeyVaultHelperConfigurationException(Resources.KeyVaultUrlNotSet);
            }

            _useMsi = _config.UseManagedServiceIdentity;
            if (!_useMsi.HasValue &&
                string.IsNullOrWhiteSpace(_config.CertificateThumbprint))
            {
                _useMsi = string.IsNullOrWhiteSpace(configManager.GetSetting(CertificateThumbprintSettingName));
            }

            // using Managed Service Identity method of authentication means we dont need any service principal arguments - appId, certificate or cert store
            if (_useMsi == true)
            {
                var azureServiceTokenProvider = new AzureServiceTokenProvider();
                _keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            }
            else
            {
                _certificateThumbprint = _config.CertificateThumbprint;
                if (string.IsNullOrWhiteSpace(_certificateThumbprint))
                {
                    _certificateThumbprint = configManager.GetSetting(CertificateThumbprintSettingName);
                    if (string.IsNullOrWhiteSpace(_certificateThumbprint))
                        throw new KeyVaultHelperConfigurationException(Resources.KeyVaultCertificateThumbprintNotSet);
                }
                _certificateThumbprint = _certificateThumbprint.Replace(" ", "").Trim();

                string certificateStoreType = _config.CertificateStoreType;
                if (string.IsNullOrWhiteSpace(certificateStoreType))
                {
                    certificateStoreType = configManager.GetSetting(CertificateStoreTypeSettingName);
                    if (string.IsNullOrWhiteSpace(certificateStoreType))
                        throw new KeyVaultHelperConfigurationException(Resources.KeyVaultCertificateStoreNotSet);
                }

                if (!Enum.TryParse(certificateStoreType, true, out _storeLocation))
                    throw new KeyVaultHelperConfigurationException(string.Format(Resources.KeyVaultCertificateStoreTypeInvalid, certificateStoreType));

                _clientId = _config.ClientId;
                if (string.IsNullOrWhiteSpace(_clientId))
                {
                    _clientId = configManager.GetSetting(ClientIdSettingName);
                    if (string.IsNullOrWhiteSpace(_clientId))
                        throw new KeyVaultHelperConfigurationException(Resources.KeyVaultClientIdNotSet);
                }

                _keyVaultClient = new KeyVaultClient(GetAccessToken);
            }
        }

        public static bool IsConfigured (out string keyVaultUrl)
        {
            ConfigManager configManager = new ConfigManager();
            keyVaultUrl = configManager.GetSetting(KeyVaultUrlSettingName);
            return !string.IsNullOrEmpty(keyVaultUrl);
        }

        public static void Configure(KeyVaultHelper.Config config)
        {
            ConfigManager configManager = new ConfigManager();

            if (string.IsNullOrWhiteSpace(config.KeyVaultUrl))
            {
                throw new KeyVaultHelperConfigurationException(Resources.KeyVaultUrlNotSet);
            }
            string keyVaultUrl = config.KeyVaultUrl.Trim();
            configManager.SetSetting(KeyVaultUrlSettingName, keyVaultUrl);

            // if msi is not specified or value is false retrieve the cert details
            if (config.UseManagedServiceIdentity != true)
            {
                if (string.IsNullOrWhiteSpace(config.CertificateThumbprint))
                {
                    throw new KeyVaultHelperConfigurationException(Resources.KeyVaultCertificateThumbprintNotSet);
                }
                var certificateThumbprint = config.CertificateThumbprint.Replace(" ", "").Trim();
                configManager.SetSetting(CertificateThumbprintSettingName, certificateThumbprint);

                if (string.IsNullOrWhiteSpace(config.CertificateStoreType))
                {
                    throw new KeyVaultHelperConfigurationException(Resources.KeyVaultCertificateStoreNotSet);
                }
                string certificateStoreType = config.CertificateStoreType.Trim();

                if (!Enum.TryParse(certificateStoreType, true, out StoreLocation storeLocation))
                    throw new KeyVaultHelperConfigurationException(string.Format(Resources.KeyVaultCertificateStoreTypeInvalid, certificateStoreType));
                configManager.SetSetting(CertificateStoreTypeSettingName, certificateStoreType);

                if (string.IsNullOrWhiteSpace(config.ClientId))
                {
                    throw new KeyVaultHelperConfigurationException(Resources.KeyVaultClientIdNotSet);
                }
                string clientId = config.ClientId.Trim();
                configManager.SetSetting(ClientIdSettingName, clientId);
            }
            _config = config;
        }

        public static KeyVaultHelper KeyVault
        {
            get
            {
                if (_instance == null)
                    _instance = new KeyVaultHelper();
                return _instance;
            }
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentNullException("secretName");
            }

            string fixedSecretName = FixSecretName(secretName);

            var secret = await _keyVaultClient.GetSecretAsync(_keyVaultUrl, fixedSecretName).ConfigureAwait(false);

            return secret.Value;
        }

        public async Task<string> SetSecretAsync(string secretName, string secretValue)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentNullException("secretName");
            }
            // KeyVault doesn't allow dots in key name, allows only alphanumeric and dashes. Replace dots with dash
            string fixedSecretName = FixSecretName(secretName);

            var secret = await _keyVaultClient.SetSecretAsync(_keyVaultUrl, fixedSecretName, secretValue);
            return secret.Value;
        }

        public async Task<string> DeleteSecretAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentNullException("secretName");
            }
            // KeyVault doesn't allow dots in key name, allows only alphanumeric and dashes. Replace dots with dash
            string fixedSecretName = FixSecretName (secretName);

            var deletedSecretResult = await _keyVaultClient.DeleteSecretAsync(_keyVaultUrl, fixedSecretName);
            return deletedSecretResult.Value;
        }

        private string FixSecretName (string secretName)
        {
            // KeyVault doesn't allow dots or slashes in key name, allows only alphanumeric and dashes. Replace dots and slashes with dash
            return secretName.Replace('.', '-').Replace('/', '-');
        }
        private async Task<string> GetAccessToken(string authority, string resource, string scope)
        {
            // get new token if needed or current token expires soon
            if (_accessToken == null || 
                _expiration == null || 
                _expiration.UtcDateTime > DateTime.Now.AddMinutes(1).ToUniversalTime())
            {
                var context = new AuthenticationContext(authority);

                var cert = RetrieveCertificate();
                var clientAssertionCertificate = new ClientAssertionCertificate(_clientId, cert);

                var result = await context.AcquireTokenAsync(resource, clientAssertionCertificate);
                _expiration = result.ExpiresOn;
                _accessToken = result.AccessToken;
            }
            return _accessToken;
        }

        private X509Certificate2 RetrieveCertificate()
        {
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(_storeLocation);
                certStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var userCertCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, _certificateThumbprint, false);

                if (userCertCollection?.Count == 0)
                {
                    throw new KeyVaultHelperConfigurationException(string.Format(Resources.KeyVaultCertificateNotFound,
                        _certificateThumbprint, 
                        certStore.Location, 
                        certStore.Name
                        ));
                }
                return userCertCollection[0];
            }
            catch (KeyVaultHelperConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new KeyVaultHelperConfigurationException(string.Format(Resources.KeyVaultErrorAccessingCertificateStore, 
                    _storeLocation
                    ), ex);
            }
            finally
            {
                certStore?.Close();
            }
        }

        // Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (_keyVaultClient != null && _instance != null)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    _keyVaultClient.Dispose();
                }
            }
            _instance = null;
        }
    }
}
