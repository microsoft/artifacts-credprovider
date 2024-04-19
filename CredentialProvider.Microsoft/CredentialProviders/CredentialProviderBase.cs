// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.CredentialProviders
{
    /// <summary>
    /// Represents a base class for credential providers.
    /// </summary>
    public abstract class CredentialProviderBase : ICredentialProvider
    {
        /// <summary>
        /// A <see cref="GetAuthenticationCredentialsResponse"/> for when credentials could not be retrieved.
        /// </summary>
        protected static readonly GetAuthenticationCredentialsResponse NotFoundGetAuthenticationCredentialsResponse = new GetAuthenticationCredentialsResponse(
            username: null,
            password: null,
            message: null,
            authenticationTypes: null,
            responseCode: MessageResponseCode.NotFound);

        /// <summary>
        /// Initializes a new instance of the <see cref="CredentialProviderBase"/> class.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> to use for logging.</param>
        /// <param name="vstsSessionTokenProvider"> A <see cref="IAzureDevOpsSessionTokenFromBearerTokenProvider"/> to use to get the ado session token</param>
        protected CredentialProviderBase(ILogger logger, IAzureDevOpsSessionTokenFromBearerTokenProvider vstsSessionTokenProvider)
        {
            Logger = logger;
            VstsSessionTokenProvider = vstsSessionTokenProvider;
        }

        public virtual bool IsCachable { get { return true; } }

        protected abstract string LoggingName { get; }

        /// <summary>
        /// Gets a <see cref="ILogger"/> to use for logging.
        /// </summary>
        protected ILogger Logger { get; }

        protected readonly IAzureDevOpsSessionTokenFromBearerTokenProvider VstsSessionTokenProvider;

        private const string Username = "VssSessionToken";

        /// <inheritdoc cref="ICredentialProvider.CanProvideCredentialsAsync"/>
        public abstract Task<bool> CanProvideCredentialsAsync(Uri uri);

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public virtual void Dispose()
        {
        }

        /// <inheritdoc cref="ICredentialProvider.HandleRequestAsync"/>
        public abstract Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken);

        protected async Task<GetAuthenticationCredentialsResponse> GetVstsTokenAsync(
            GetAuthenticationCredentialsRequest request,
            IEnumerable<ITokenProvider> tokenProviders,
            TokenRequest tokenRequest,
            CancellationToken cancellationToken)
        {
            // Try each bearer token provider (e.g. cache, WIA, UI, DeviceCode) in order.
            // Only consider it successful if the bearer token can be exchanged for an Azure DevOps token.
            foreach (ITokenProvider tokenProvider in tokenProviders)
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
                Info(Resources.ExchangingBearerTokenForSessionToken);
                try
                {
                    string sessionToken = await VstsSessionTokenProvider.GetAzureDevOpsSessionTokenFromBearerToken(
                        request,
                        bearerToken,
                        tokenProvider.IsInteractive,
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(sessionToken))
                    {
                        Verbose(string.Format(Resources.VSTSSessionTokenCreated, tokenRequest.Uri.AbsoluteUri));
                        return new GetAuthenticationCredentialsResponse(
                            Username,
                            sessionToken,
                            message: null,
                            authenticationTypes: ["Basic"],
                            responseCode: MessageResponseCode.Success);
                    }
                }
                catch (Exception e)
                {
                    Verbose(string.Format(Resources.VSTSCreateSessionException, tokenRequest.Uri.AbsoluteUri, e.Message, e.StackTrace));
                }
            }

            Verbose(string.Format(Resources.VSTSCredentialsNotFound, tokenRequest.Uri.AbsoluteUri));
            return null;
        }

        protected void Error(string message)
        {
            Logger.Error($"{LoggingName} - {message}");
        }

        protected void Warning(string message)
        {
            Logger.Warning($"{LoggingName} - {message}");
        }

        protected void Info(string message)
        {
            Logger.Info($"{LoggingName} - {message}");
        }

        protected void Verbose(string message)
        {
            Logger.Verbose($"{LoggingName} - {message}");
        }
    }
}