// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
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
        protected CredentialProviderBase(ILogger logger)
        {
            Logger = logger;
        }

        public virtual bool IsCachable { get { return true; } }

        protected abstract string LoggingName { get; }

        /// <summary>
        /// Gets a <see cref="ILogger"/> to use for logging.
        /// </summary>
        protected ILogger Logger { get; }

        /// <inheritdoc cref="ICredentialProvider.CanProvideCredentialsAsync"/>
        public abstract Task<bool> CanProvideCredentialsAsync(Uri uri);

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public virtual void Dispose()
        {
        }

        /// <inheritdoc cref="ICredentialProvider.HandleRequestAsync"/>
        public abstract Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken);

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