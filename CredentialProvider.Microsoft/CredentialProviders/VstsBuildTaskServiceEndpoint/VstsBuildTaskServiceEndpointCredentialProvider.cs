// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Util;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint
{
    public sealed class VstsBuildTaskServiceEndpointCredentialProvider : CredentialProviderBase
    {
        private Lazy<Dictionary<string, EndpointCredentials>> LazyCredentials;
        private Lazy<Dictionary<string, ExternalEndpointCredentials>> LazyExternalCredentials;
        private ITokenProvidersFactory TokenProvidersFactory;
        private IAuthUtil AuthUtil;

        // Dictionary that maps an endpoint string to EndpointCredentials
        private Dictionary<string, EndpointCredentials> Credentials => LazyCredentials.Value;
        private Dictionary<string, ExternalEndpointCredentials> ExternalCredentials => LazyExternalCredentials.Value;

        public VstsBuildTaskServiceEndpointCredentialProvider(ILogger logger, ITokenProvidersFactory tokenProvidersFactory, IAuthUtil authUtil)
            : base(logger)
        {
            TokenProvidersFactory = tokenProvidersFactory;
            LazyCredentials = new Lazy<Dictionary<string, EndpointCredentials>>(() =>
            {
                return FeedEndpointCredentialsParser.ParseFeedEndpointsJsonToDictionary(logger);
            });
            LazyExternalCredentials = new Lazy<Dictionary<string, ExternalEndpointCredentials>>(() =>
            {
                return FeedEndpointCredentialsParser.ParseExternalFeedEndpointsJsonToDictionary(logger);
            });
            AuthUtil = authUtil;
        }

        public override bool IsCachable { get { return false; } }

        protected override string LoggingName => nameof(VstsBuildTaskServiceEndpointCredentialProvider);

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            string feedEndPointsJson = Environment.GetEnvironmentVariable(EnvUtil.EndpointCredentials);
            string externalFeedEndPointsJson = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskExternalEndpoints);

            if (string.IsNullOrWhiteSpace(feedEndPointsJson) && string.IsNullOrWhiteSpace(externalFeedEndPointsJson))
            {
                Verbose(Resources.BuildTaskEndpointEnvVarError);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public override async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Verbose(string.Format(Resources.IsRetry, request.IsRetry));

            string uriString = request.Uri.AbsoluteUri;
            bool externalEndpointFound = ExternalCredentials.TryGetValue(uriString, out ExternalEndpointCredentials matchingExternalEndpoint);
            if (externalEndpointFound && !string.IsNullOrWhiteSpace(matchingExternalEndpoint.Password))
            {
                Verbose(string.Format(Resources.BuildTaskEndpointMatchingUrlFound, uriString));
                return GetResponse(
                    matchingExternalEndpoint.Username,
                    matchingExternalEndpoint.Password,
                    null,
                    MessageResponseCode.Success);
            }

            bool endpointFound = Credentials.TryGetValue(uriString, out EndpointCredentials matchingEndpoint);
            if (endpointFound && !string.IsNullOrWhiteSpace(matchingEndpoint.ClientId))
            {
                var authInfo = await AuthUtil.GetAuthorizationInfoAsync(request.Uri, cancellationToken);
                Verbose(string.Format(Resources.UsingAuthority, authInfo.EntraAuthorityUri));
                Verbose(string.Format(Resources.UsingTenant, authInfo.EntraTenantId));

                var clientCertificate = GetCertificate(matchingEndpoint);
                Info(clientCertificate == null
                    ? (Resources.ClientCertificateNotFound)
                    : string.Format(Resources.UsingCertificate, clientCertificate.Subject));

                IEnumerable<ITokenProvider> tokenProviders = await TokenProvidersFactory.GetAsync(authInfo.EntraAuthorityUri);
                cancellationToken.ThrowIfCancellationRequested();

                var tokenRequest = new TokenRequest()
                {
                    IsRetry = request.IsRetry,
                    IsNonInteractive = true,
                    CanShowDialog = false,
                    IsWindowsIntegratedAuthEnabled = false,
                    InteractiveTimeout = TimeSpan.FromSeconds(EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(Logger)),
                    ClientId = matchingEndpoint.ClientId,
                    ClientCertificate = clientCertificate,
                    TenantId = authInfo.EntraTenantId
                };

                foreach(var tokenProvider in tokenProviders)
                {
                    bool shouldRun = tokenProvider.CanGetToken(tokenRequest);
                    if (!shouldRun)
                    {
                        Verbose(string.Format(Resources.NotRunningBearerTokenProvider, tokenProvider.Name));
                        continue;
                    }

                    Verbose(string.Format(Resources.AttemptingToAcquireBearerTokenUsingProvider, tokenProvider.Name));

                    string bearerToken;
                    try
                    {
                        var result = await tokenProvider.GetTokenAsync(tokenRequest, cancellationToken);
                        bearerToken = result?.AccessToken;
                    }
                    catch (Exception ex)
                    {
                        Verbose(string.Format(Resources.BearerTokenProviderException, tokenProvider.Name, ex));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(bearerToken))
                    {
                        Verbose(string.Format(Resources.BearerTokenProviderReturnedNull, tokenProvider.Name));
                        continue;
                    }

                    Info(string.Format(Resources.AcquireBearerTokenSuccess, tokenProvider.Name));
                    return GetResponse(
                        matchingEndpoint.ClientId,
                        bearerToken,
                        null,
                        MessageResponseCode.Success);
                }
            }

            Verbose(string.Format(Resources.BuildTaskEndpointNoMatchingUrl, uriString));
            return GetResponse(
                null,
                null,
                string.Format(Resources.BuildTaskFailedToAuthenticate, uriString),
                MessageResponseCode.Error);
        }

        private GetAuthenticationCredentialsResponse GetResponse(string username, string password, string message, MessageResponseCode responseCode)
        {
            return new GetAuthenticationCredentialsResponse(
                    username: username,
                    password: password,
                    message: message,
                    authenticationTypes: new List<string>
                    {
                        "Basic"
                    },
                    responseCode: responseCode);
        }

        private X509Certificate2 GetCertificate(EndpointCredentials credentials)
        {
            if (!string.IsNullOrWhiteSpace(credentials.CertificateSubjectName))
            {
                return CertificateUtil.GetCertificateBySubjectName(Logger, credentials.CertificateSubjectName);
            }

            if (!string.IsNullOrWhiteSpace(credentials.CertificateFilePath))
            {
                return CertificateUtil.GetCertificateByFilePath(Logger, credentials.CertificateFilePath);
            }

            return null;
        }
    }
}
