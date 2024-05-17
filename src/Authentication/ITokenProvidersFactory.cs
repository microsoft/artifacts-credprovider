// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Artifacts.Authentication;

public interface ITokenProvidersFactory
{
    Task<IEnumerable<ITokenProvider>> GetAsync(Uri authority);
}
