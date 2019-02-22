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
            Stopwatch timer = new Stopwatch();
            timer.Start();

            Connection = connection;
            CancellationToken = cancellationToken;

            TRequest request = MessageUtilities.DeserializePayload<TRequest>(message);

            try {
                TResponse response = null;
                Logger.Verbose(string.Format(Resources.HandlingRequest, message.Type, message.Method, timer.ElapsedMilliseconds, message.Payload.ToString(Formatting.None)));
                try {
                    using (GetProgressReporter(connection, message, cancellationToken))
                    {
                        response = await HandleRequestAsync(request).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    // NuGet will handle canceling event but verbose logs in this case might be interesting.
                    Logger.Verbose(string.Format(Resources.RequestHandlerCancelingExceptionMessage, ex.InnerException, ex.Message));
                    return;
                }
                Logger.Verbose(string.Format(Resources.SendingResponse, message.Type, message.Method, timer.ElapsedMilliseconds));
                // If we did not send a cancel message, we must submit the response even if cancellationToken is canceled.
                await responseHandler.SendResponseAsync(message, response, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

                Logger.Verbose(string.Format(Resources.TimeElapsedAfterSendingResponse, message.Type, message.Method, timer.ElapsedMilliseconds));
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
                    Logger.Verbose(Resources.ShuttingDown);
                    return false;
                }

                Logger.Verbose(string.Format(Resources.ResponseHandlerException, message.Method, message.RequestId));
                Logger.Verbose(ex.ToString());
                return false;
            }

            CancellationToken = CancellationToken.None;
            timer.Stop();
        }

        public abstract Task<TResponse> HandleRequestAsync(TRequest request);

        protected virtual AutomaticProgressReporter GetProgressReporter(IConnection connection, Message message, CancellationToken cancellationToken)
        {
            return null;
        }
    }
}