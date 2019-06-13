// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.RequestHandlers
{
    /// <summary>
    /// Handles a <see cref="ExtendedGetAuthenticationCredentialsRequest"/> and replies with credentials.
    /// Delegates to a <see cref="IGetAuthenticationCredentialsOrchestrator"/>
    /// </summary>
    internal class GetAuthenticationCredentialsRequestHandler : RequestHandlerBase<GetAuthenticationCredentialsRequest, GetAuthenticationCredentialsResponse>
    {
        private readonly TimeSpan progressReporterTimeSpan = TimeSpan.FromSeconds(2);
        private readonly IGetAuthenticationCredentialsOrchestrator orchestrator;

        public GetAuthenticationCredentialsRequestHandler(
            IGetAuthenticationCredentialsOrchestrator orchestrator,
            ILogger logger)
            : base(logger)
        {
            this.orchestrator = orchestrator;
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request)
        {
            return orchestrator.HandleRequestAsync(
                request,
                skipValidatingCachedCreds: true /* Plugin uses IsRetry flow */,
                CancellationToken);
        }

        protected override AutomaticProgressReporter GetProgressReporter(IConnection connection, Message message, CancellationToken cancellationToken)
        {
            Logger.Verbose(string.Format(Resources.CreatingProgressReporter, progressReporterTimeSpan.ToString()));
            return AutomaticProgressReporter.Create(connection, message, progressReporterTimeSpan, cancellationToken);
        }
    }

    internal interface IGetAuthenticationCredentialsOrchestrator
    {
        Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(
            GetAuthenticationCredentialsRequest request,
            bool skipValidatingCachedCreds,
            CancellationToken cancellationToken);
    }


    /// <summary>
    /// Orchestrates getting credentials from multiple credential providers
    /// </summary>
    internal class GetAuthenticationCredentialsOrchestrator : IGetAuthenticationCredentialsOrchestrator
    {
        private readonly ICache<Uri, string> cache;
        private readonly ILogger logger;
        private readonly IReadOnlyCollection<ICredentialProvider> credentialProviders;
        private readonly ICachedCredentialsValidator credentialsValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetAuthenticationCredentialsRequestHandler"/> class.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> to use for logging.</param>
        /// <param name="credentialProviders">An <see cref="IReadOnlyCollection{ICredentialProviders}"/> containing credential providers.</param>
        /// <param name="cache">An <see cref="ICache{TKey, TValue}"/> cache to store found credentials.</param>
        public GetAuthenticationCredentialsOrchestrator(
            ICache<Uri, string> cache,
            ILogger logger,
            IReadOnlyCollection<ICredentialProvider> credentialProviders,
            ICachedCredentialsValidator credentialsValidator)
        {
            this.cache = cache;
            this.logger = logger;
            this.credentialProviders = credentialProviders;
            this.credentialsValidator = credentialsValidator;
        }

        public async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(
            GetAuthenticationCredentialsRequest request,
            bool skipValidatingCachedCreds,
            CancellationToken cancellationToken)
        {
            logger.Verbose(string.Format(Resources.HandlingAuthRequest, request.Uri, request.IsRetry, request.IsNonInteractive, request.CanShowDialog));

            if (request?.Uri == null)
            {
                return new GetAuthenticationCredentialsResponse(
                    username: null,
                    password: null,
                    message: Resources.RequestUriNull,
                    authenticationTypes: null,
                    responseCode: MessageResponseCode.Error);
            }

            logger.Verbose(string.Format(Resources.Uri, request.Uri));

            foreach (ICredentialProvider credentialProvider in credentialProviders)
            {
                if (await credentialProvider.CanProvideCredentialsAsync(request.Uri) == false)
                {
                    logger.Verbose(string.Format(Resources.SkippingCredentialProvider, credentialProvider, request.Uri.ToString()));
                    continue;
                }

                if (credentialProvider.IsCachable && TryCache(request, out string cachedToken))
                {
                    var response = new GetAuthenticationCredentialsResponse(
                        username: "VssSessionToken",
                        password: cachedToken,
                        message: null,
                        authenticationTypes: new List<string> { "Basic" },
                        responseCode: MessageResponseCode.Success);

                    if (skipValidatingCachedCreds)
                    {
                        // This may return expired/revoked/etc tokens
                        return response;
                    }

                    // Validate that the cached token is still valid by making a GET with the credentials
                    if (await credentialsValidator.ValidateAsync(request.Uri, response.Username, response.Password))
                    {
                        return response;
                    }

                    // Invalidate the invalid credentials and continue with obtaining new ones
                    InvalidateCache(request.Uri);
                }

                try
                {
                    GetAuthenticationCredentialsResponse response = await credentialProvider.HandleRequestAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    if (response != null && response.ResponseCode == MessageResponseCode.Success)
                    {
                        if (cache != null && credentialProvider.IsCachable)
                        {
                            logger.Verbose(string.Format(Resources.CachingSessionToken, request.Uri.ToString()));
                            cache[request.Uri] = response.Password;
                        }

                        return response;
                    }
                    else if (!string.IsNullOrWhiteSpace(response?.Message))
                    {
                        logger.Verbose(response.Message);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(string.Format(Resources.AcquireSessionTokenFailed, e.ToString()));

                    return new GetAuthenticationCredentialsResponse(
                        username: null,
                        password: null,
                        message: e.Message,
                        authenticationTypes: null,
                        responseCode: MessageResponseCode.Error);
                }
            }

            return new GetAuthenticationCredentialsResponse(
                username: null,
                password: null,
                message: null,
                authenticationTypes: null,
                responseCode: MessageResponseCode.NotFound);
        }


        private bool TryCache(GetAuthenticationCredentialsRequest request, out string cachedToken)
        {
            cachedToken = null;

            logger.Verbose(string.Format(Resources.IsRetry, request.IsRetry));
            if (request.IsRetry)
            {
                InvalidateCache(request.Uri);
                return false;
            }
            else if (cache.TryGetValue(request.Uri, out string password))
            {
                logger.Verbose(string.Format(Resources.FoundCachedSessionToken, request.Uri.ToString()));
                cachedToken = password;
                return true;
            }

            logger.Verbose(string.Format(Resources.CachedSessionTokenNotFound, request.Uri.ToString()));
            return false;
        }

        private void InvalidateCache(Uri requestUri)
        {
            logger.Verbose(string.Format(Resources.InvalidatingCachedSessionToken, requestUri.ToString()));
            cache?.Remove(requestUri);
        }
    }
}