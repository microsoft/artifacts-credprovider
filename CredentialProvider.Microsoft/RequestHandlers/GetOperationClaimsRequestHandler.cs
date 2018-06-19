// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.RequestHandlers
{
    /// <summary>
    /// Handles a <see cref="GetOperationClaimsRequest"/> and replies with the supported operations.
    /// </summary>
    internal class GetOperationClaimsRequestHandler : RequestHandlerBase<GetOperationClaimsRequest, GetOperationClaimsResponse>
    {
        /// <summary>
        /// A <see cref="GetOperationClaimsResponse"/> when a registered credential provider can provide credentials for the current request.
        /// </summary>
        private static readonly GetOperationClaimsResponse CanProvideCredentialsResponse = new GetOperationClaimsResponse(new List<OperationClaim>
        {
            OperationClaim.Authentication
        });

        /// <summary>
        /// A <see cref="GetOperationClaimsResponse"/> when no registered credential providers can provide credentials for the current request.
        /// </summary>
        private static readonly GetOperationClaimsResponse EmptyGetOperationClaimsResponse = new GetOperationClaimsResponse(new List<OperationClaim>());

        private readonly IReadOnlyCollection<ICredentialProvider> _credentialProviders;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetOperationClaimsRequestHandler"/> class.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> to use for logging.</param>
        /// <param name="credentialProviders">An <see cref="IReadOnlyCollection{ICredentialProviders}"/> containing credential providers.</param>
        public GetOperationClaimsRequestHandler(ILogger logger, IReadOnlyCollection<ICredentialProvider> credentialProviders)
            : base(logger)
        {
            _credentialProviders = credentialProviders ?? throw new ArgumentNullException(nameof(credentialProviders));
        }

        public override Task<GetOperationClaimsResponse> HandleRequestAsync(GetOperationClaimsRequest request)
        {
            if (request.PackageSourceRepository != null || request.ServiceIndex != null)
            {
                return Task.FromResult(EmptyGetOperationClaimsResponse);
            }

            return Task.FromResult(CanProvideCredentialsResponse);
        }
    }
}