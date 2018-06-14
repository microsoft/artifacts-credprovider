// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.RequestHandlers
{
    /// <summary>
    /// Handles a <see cref="SetCredentialsRequest"/>
    /// </summary>
    internal class SetCredentialsRequestHandler : RequestHandlerBase<SetCredentialsRequest, SetCredentialsResponse>
    {
        private static readonly SetCredentialsResponse SuccessResponse = new SetCredentialsResponse(MessageResponseCode.Success);

        public SetCredentialsRequestHandler(ILogger logger)
            : base(logger)
        {
        }

        public override Task<SetCredentialsResponse> HandleRequestAsync(SetCredentialsRequest request)
        {
            // There's currently no way to handle proxies, so nothing we can do here
            return Task.FromResult(SuccessResponse);
        }
    }
}