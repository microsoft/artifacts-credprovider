// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Artifacts.Authentication;

public static class MsalConstants
{
    private const string AzureDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798/.default";
    public static readonly IEnumerable<string> AzureDevOpsScopes = Array.AsReadOnly(new[] { AzureDevOpsResource });

    public static readonly Guid FirstPartyTenant = Guid.Parse("f8cdef31-a31e-4b4a-93e4-5f571e91255a");
    public static readonly Guid MsaAccountTenant = Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad");
}
