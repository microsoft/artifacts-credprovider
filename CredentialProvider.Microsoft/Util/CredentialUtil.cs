// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace NuGetCredentialProvider.Util;

internal static class CredentialUtil
{
    private const string ADOSelfDescribingTokenIssuer = "YXBwLnZzdG9rZW4udmlzdWFsc3R1ZGlvLmNvb"; // base64 for "app.vsstoken.visualstudio.com";

    public static bool IsEntraToken(string jwt)
    {
        if (string.IsNullOrEmpty(jwt))
        {
            return false;
        }

        // JWT format: header.payload.signature
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var payload = parts[1];
        return !payload.Contains(ADOSelfDescribingTokenIssuer);
    }
}
