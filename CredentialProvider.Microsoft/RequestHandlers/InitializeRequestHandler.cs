// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.RequestHandlers
{
    /// <summary>
    /// Handles an <see cref="InitializeRequest"/>.
    /// </summary>
    internal class InitializeRequestHandler : RequestHandlerBase<InitializeRequest, InitializeResponse>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InitializeRequestHandler"/> class.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> to use for logging.</param>
        public InitializeRequestHandler(ILogger logger)
            : base(logger)
        {
        }

        public override Task<InitializeResponse> HandleRequestAsync(InitializeRequest request)
        {
            return Task.FromResult(new InitializeResponse(MessageResponseCode.Success));
        }
    }
}