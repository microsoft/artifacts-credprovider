// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.RequestHandlers
{
    /// <summary>
    /// A base class for implementations of <see cref="IRequestHandler"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message.</typeparam>
    /// <typeparam name="TResponse">The type of the response message.</typeparam>
    internal abstract class RequestHandlerBase<TRequest, TResponse> : IRequestHandler
        where TResponse : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandlerBase{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> to use for logging.</param>
        protected RequestHandlerBase(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> to use.
        /// </summary>
        public virtual CancellationToken CancellationToken { get; private set; } = CancellationToken.None;

        /// <summary>
        /// Gets the current <see cref="IConnection"/>.
        /// </summary>
        public IConnection Connection { get; private set; }

        /// <summary>
        /// Gets the current <see cref="ILogger"/> to use for logging.
        /// </summary>
        public ILogger Logger { get; }

        /// <inheritdoc cref="IRequestHandler.HandleResponseAsync"/>
        public async Task HandleResponseAsync(IConnection connection, Message message, IResponseHandler responseHandler, CancellationToken cancellationToken)
        {
            Connection = connection;
            CancellationToken = cancellationToken;

            TRequest request = MessageUtilities.DeserializePayload<TRequest>(message);

            Logger.Verbose(string.Format(Resources.HandlingRequest, message.Type, message.Method, message.Payload.ToString(Formatting.None)));

            TResponse response = null;
            using (GetProgressReporter(connection, message, cancellationToken))
            {
                response = await HandleRequestAsync(request).ConfigureAwait(continueOnCapturedContext: false);
            }

            // We don't want to print credentials on auth responses
            if (message.Method != MessageMethod.GetAuthenticationCredentials)
            {
                var logResponse = JsonConvert.SerializeObject(response, new JsonSerializerSettings { Formatting = Formatting.None });
                Logger.Verbose(string.Format(Resources.SendingResponse, logResponse));
            }

            await responseHandler.SendResponseAsync(message, response, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            CancellationToken = CancellationToken.None;
        }

        public abstract Task<TResponse> HandleRequestAsync(TRequest request);

        protected virtual AutomaticProgressReporter GetProgressReporter(IConnection connection, Message message, CancellationToken cancellationToken)
        {
            return null;
        }
    }
}