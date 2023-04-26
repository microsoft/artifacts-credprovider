// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public interface ITokenProvider
{
    string Name { get; }

    bool IsInteractive { get; }

    bool CanGetToken(TokenRequest tokenRequest);

    Task<AuthenticationResult?> GetTokenAsync(TokenRequest tokenRequest, CancellationToken cancellationToken = default);
}
