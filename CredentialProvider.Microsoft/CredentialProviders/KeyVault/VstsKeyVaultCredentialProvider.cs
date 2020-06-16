// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using Microsoft.Azure.KeyVault.Helper;
using ILogger = NuGetCredentialProvider.Logging.ILogger;
using System.Collections.Generic;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders.KeyVault
{
    [DataContract]
    public class CredentialProviderResponse
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Password { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string Message { get; set; }
    }

    public sealed class VstsKeyVaultCredentialProvider : CredentialProviderBase
    {
        private const string PackagesDevAzureCom = "pkgs.dev.azure.com";
        private const string DevAzureCom = "dev.azure.com";
        private const string Username = "PersonalAccessToken";

        protected override string LoggingName => nameof(VstsKeyVaultCredentialProvider);

        public VstsKeyVaultCredentialProvider (ILogger logger)
            : base(logger)
        {
            string keyVaultUrlEnvVar = Environment.GetEnvironmentVariable(EnvUtil.KeyVaultUrlEnvVar);
            if (!string.IsNullOrEmpty(keyVaultUrlEnvVar))
            {
                KeyVaultHelper.Configure(new KeyVaultHelper.Config()
                {
                    KeyVaultUrl = keyVaultUrlEnvVar,
                    UseManagedServiceIdentity = true
                });
            }
        }

        public override bool IsCachable { get { return false; } }

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            bool isConfigured = KeyVaultHelper.IsConfigured(out string keyVaultUrl);
            Verbose(isConfigured ? string.Format(Resources.KeyVaultIsConfigured, LoggingName, keyVaultUrl) : string.Format(Resources.KeyVaultNotConfigured, LoggingName));
            return Task.FromResult<bool>(isConfigured);
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = request.Uri;
            var host = uri.Host.ToLower();
            // for pkgs.dev.azure.com, add account before the host
            if ((host == PackagesDevAzureCom ||
                 host == DevAzureCom) &&
                uri.Segments.Length > 1)
            {
                var path = uri.Segments[1];
                var account = path.Trim(new char[] { '\\', '/' });
                host = account + "-" + host;
            }

            try
            {
                string patToken = Task.Run(async () =>
                {
                    return await KeyVaultHelper.KeyVault.GetSecretAsync(host);
                }).Result;

                if (!TryParseTokenFromCredProviderOutput(patToken, out string userName, out string password))
                {
                    password = patToken;
                }

                if (!string.IsNullOrWhiteSpace(password))
                {
                    Verbose(string.Format(Resources.VSTSSessionTokenCreated, request.Uri.ToString()));
                    return GetResponse(
                        Username,
                        password,
                        message: null,
                        responseCode: MessageResponseCode.Success);
                }
            }
            catch (OperationCanceledException oex)
            {
                throw oex;
            }
            catch (Exception ex)
            {
                if (ex is AggregateException)
                    ex = ex.GetBaseException();

                if (ex is KeyVaultErrorException exception && exception.Body.Error.Code == "SecretNotFound")
                {
                    if (uri.Host.ToLower() == PackagesDevAzureCom)
                    {
                        // retry with just host
                        Verbose(string.Format(Resources.KeyVaultCouldNotRetrievePatRetrying, host, uri.Host));
                        return HandleRequestAsync(new GetAuthenticationCredentialsRequest(new Uri(uri.Scheme + "://" + uri.Host), false, true, false), cancellationToken);
                    }
                    Verbose(string.Format(Resources.KeyVaultCredentialNotFound, host));
                    return GetResponse(
                        null,
                        null,
                        message: null,
                        responseCode: MessageResponseCode.NotFound);
                }
                Verbose(string.Format(Resources.VSTSCreateSessionException, request.Uri, ex.Message, ex.StackTrace));
                return GetResponse(
                        null,
                        null,
                        message: null,
                        responseCode: MessageResponseCode.Error);
            }
            Verbose(string.Format(Resources.VSTSCredentialsNotFound, request.Uri.ToString()));

            // case for empty token returned
            return GetResponse(
                null,
                null,
                message: null,
                responseCode: MessageResponseCode.NotFound);
        }

        private static bool TryParseTokenFromCredProviderOutput(string output, out string userName, out string token)
        {
            userName = null;
            token = null;

            try
            {
                var response = JsonConvert.DeserializeObject<CredentialProviderResponse>(output);
                if (response != null)
                {
                    // In this case the password returned is expected to be a TOKEN and NOT a raw password
                    userName = response.Username;
                    token = response.Password;
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private Task<GetAuthenticationCredentialsResponse> GetResponse(string username, string password, string message, MessageResponseCode responseCode)
        {
            return Task.FromResult(new GetAuthenticationCredentialsResponse(
                    username: username,
                    password: password,
                    message: message,
                    authenticationTypes: new List<string>
                    {
                        "Basic"
                    },
                    responseCode: responseCode));
        }
    }
}
