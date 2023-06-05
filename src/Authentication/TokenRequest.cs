// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class TokenRequest
{
    public TokenRequest(Uri uri)
    {
        this.Uri = uri ?? throw new ArgumentNullException(nameof(uri));
    }

    public Uri Uri { get; }

    public bool IsRetry { get; set; }

    public bool IsInteractive { get; set; }

    // Provided for back-compat to make migration easier
    public bool IsNonInteractive { get => !IsInteractive; set => IsInteractive = !value; }

    public bool CanShowDialog { get; set; } = true;

    public bool IsWindowsIntegratedAuthEnabled { get; set; } = true;

    public string? LoginHint { get; set; } = null;

    public TimeSpan InteractiveTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public Func<DeviceCodeResult, Task>? DeviceCodeResultCallback { get; set; } = null;
}
