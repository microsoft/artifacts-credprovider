// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Diagnostics;
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

            try {
                TResponse response = null;
                Logger.Verbose(string.Format(Resources.HandlingRequest, message.Type, message.Method, message.Payload.ToString(Formatting.None)));
                try {
                    using (GetProgressReporter(connection, message, cancellationToken))
                    {
                        response = await HandleRequestAsync(request).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    // We have been canceled by NuGet. Send a cancellation response.
                    var cancelMessage = MessageUtilities.Create(message.RequestId, MessageType.Cancel, message.Method);
                    await connection.SendAsync(cancelMessage, CancellationToken.None);

                    Logger.Verbose(ex.ToString());

                    // We must guarantee that exactly one terminating message is sent, so do not fall through to send
                    // the normal response, but also do not rethrow.
                    return;
                }
                // If we did not send a cancel message, we must submit the response even if cancellationToken is canceled.
                await responseHandler.SendResponseAsync(message, response, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception ex) when (LogExceptionAndReturnFalse(ex))
            {
                throw;
            }

            bool LogExceptionAndReturnFalse(Exception ex)
            {
                // don't report cancellations during shutdown, they're most likely not interesting.
                if (ex is OperationCanceledException && Program.IsShuttingDown && !Debugger.IsAttached)
                {
                    return false;
                }

                Logger.Verbose(string.Format(Resources.ResponseHandlerException, message.Method, message.RequestId));
                Logger.Verbose(ex.ToString());
                return false;
            }

            CancellationToken = CancellationToken.None;
        }

        public abstract Task<TResponse> HandleRequestAsync(TRequest request);

        protected virtual AutomaticProgressReporter GetProgressReporter(IConnection connection, Message message, CancellationToken cancellationToken)
        {
            return null;
        }
    }
}