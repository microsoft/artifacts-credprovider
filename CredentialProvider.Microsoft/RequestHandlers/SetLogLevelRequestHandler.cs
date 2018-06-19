// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.RequestHandlers
{
    internal class SetLogLevelRequestHandler : RequestHandlerBase<SetLogLevelRequest, SetLogLevelResponse>
    {
        private static readonly SetLogLevelResponse SuccessResponse = new SetLogLevelResponse(MessageResponseCode.Success);

        public SetLogLevelRequestHandler(ILogger logger)
            : base(logger)
        {
        }

        public override Task<SetLogLevelResponse> HandleRequestAsync(SetLogLevelRequest request)
        {
            Logger.SetLogLevel(request.LogLevel);
            return Task.FromResult(SuccessResponse);
        }
    }
}
