// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Security.Cryptography.X509Certificates;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class TokenRequest
{
    [Obsolete($"The uri parameter is unused and unnecessary. Use the parameterless constructor instead.")]
    public TokenRequest(Uri uri)
    {
    }

    public TokenRequest()
    {
    }

    public bool IsRetry { get; set; }

    public bool IsInteractive { get; set; }

    // Provided for back-compat to make migration easier
    public bool IsNonInteractive { get => !IsInteractive; set => IsInteractive = !value; }

    public bool CanShowDialog { get; set; } = true;

    public bool IsWindowsIntegratedAuthEnabled { get; set; } = true;

    public string? LoginHint { get; set; } = null;

    public TimeSpan InteractiveTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public Func<DeviceCodeResult, Task>? DeviceCodeResultCallback { get; set; } = null;

    public string? ClientId { get; set; } = null;

    public string? TenantId { get; set; } = null;

    public X509Certificate2? ClientCertificate { get; set; } = null;
}
