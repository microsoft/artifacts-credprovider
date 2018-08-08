// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.RequestHandlers
{
    /// <summary>
    /// Handles a <see cref="GetAuthenticationCredentialsRequest"/> and replies with credentials.
    /// </summary>
    internal class GetAuthenticationCredentialsRequestHandler : RequestHandlerBase<GetAuthenticationCredentialsRequest, GetAuthenticationCredentialsResponse>
    {
        private readonly ICache<Uri, string> cache;
        private readonly IReadOnlyCollection<ICredentialProvider> credentialProviders;
        private readonly TimeSpan progressReporterTimeSpan = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="GetAuthenticationCredentialsRequestHandler"/> class.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> to use for logging.</param>
        /// <param name="credentialProviders">An <see cref="IReadOnlyCollection{ICredentialProviders}"/> containing credential providers.</param>
        /// <param name="cache">An <see cref="ICache{TKey, TValue}"/> cache to store found credentials.</param>
        public GetAuthenticationCredentialsRequestHandler(ILogger logger, IReadOnlyCollection<ICredentialProvider> credentialProviders, ICache<Uri, string> cache)
            : base(logger)
        {
            this.credentialProviders = credentialProviders ?? throw new ArgumentNullException(nameof(credentialProviders));
            this.cache = cache;
        }

        public GetAuthenticationCredentialsRequestHandler(ILogger logger, IReadOnlyCollection<ICredentialProvider> credentialProviders)
            : this(logger, credentialProviders, null)
        {
            this.cache = GetSessionTokenCache(logger);
        }

        public override async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request)
        {
            Logger.Verbose(string.Format(Resources.HandlingAuthRequest, request.Uri, request.IsRetry, request.IsNonInteractive, request.CanShowDialog));

            if (request?.Uri == null)
            {

                return new GetAuthenticationCredentialsResponse(
                    username: null,
                    password: null,
                    message: Resources.RequestUriNull,
                    authenticationTypes: null,
                    responseCode: MessageResponseCode.Error);
            }

            Logger.Verbose(string.Format(Resources.Uri, request.Uri));

            if (TryCache(request, out string cachedToken))
            {
                return new GetAuthenticationCredentialsResponse(
                    username: "VssSessionToken",
                    password: cachedToken,
                    message: null,
                    authenticationTypes: new List<string> { "Basic" },
                    responseCode: MessageResponseCode.Success);
            }

            foreach (ICredentialProvider credentialProvider in credentialProviders)
            {
                if (await credentialProvider.CanProvideCredentialsAsync(request.Uri) == false)
                {
                    Logger.Verbose(string.Format(Resources.SkippingCredentialProvider, credentialProvider, request.Uri.ToString()));
                    continue;
                }

                try
                {
                    GetAuthenticationCredentialsResponse response = await credentialProvider.HandleRequestAsync(request, CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    if (response != null && response.ResponseCode == MessageResponseCode.Success)
                    {
                        if (cache != null && credentialProvider.IsCachable)
                        {
                            Logger.Verbose(string.Format(Resources.CachingSessionToken, request.Uri.ToString()));
                            cache[request.Uri] = response.Password;
                        }

                        return response;
                    }
                    else if (!string.IsNullOrWhiteSpace(response?.Message))
                    {
                        Logger.Verbose(response.Message);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(string.Format(Resources.AcquireSessionTokenFailed, e.ToString()));

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

        protected override AutomaticProgressReporter GetProgressReporter(IConnection connection, Message message, CancellationToken cancellationToken)
        {
            Logger.Verbose(string.Format(Resources.CreatingProgressReporter, progressReporterTimeSpan.ToString()));
            return AutomaticProgressReporter.Create(connection, message, progressReporterTimeSpan, cancellationToken);
        }

        private static ICache<Uri, string> GetSessionTokenCache(ILogger logger)
        {
            if (EnvUtil.SessionTokenCacheEnabled())
            {
                logger.Verbose(string.Format(Resources.SessionTokenCacheLocation, EnvUtil.SessionTokenCacheLocation));
                return new SessionTokenCache(EnvUtil.SessionTokenCacheLocation);
            }

            logger.Verbose(Resources.SessionTokenCacheDisabled);
            return new NoOpCache<Uri, string>();
        }

        private bool TryCache(GetAuthenticationCredentialsRequest request, out string cachedToken)
        {
            cachedToken = null;

            Logger.Verbose(string.Format(Resources.IsRetry, request.IsRetry));
            if (request.IsRetry)
            {
                Logger.Verbose(string.Format(Resources.InvalidatingCachedSessionToken, request.Uri.ToString()));
                cache?.Remove(request.Uri);
                return false;
            }
            else if (cache.TryGetValue(request.Uri, out string password))
            {
                Logger.Verbose(string.Format(Resources.FoundCachedSessionToken, request.Uri.ToString()));
                cachedToken = password;
                return true;
            }

            Logger.Verbose(string.Format(Resources.CachedSessionTokenNotFound, request.Uri.ToString()));
            return false;
        }
    }
}