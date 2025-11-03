// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace Microsoft.Artifacts.Authentication;

public static class AzureArtifacts
{
    /// <summary>
    /// Azure Artifacts application ID.
    /// </summary>
    public const string ClientId = "d5a56ea4-7369-46b8-a538-c370805301bf";

    /// <summary>
    /// Visual Studio application ID.
    /// </summary>
    private const string LegacyClientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";

    private const string MacOSXRedirectUri = "msauth.com.microsoft.azureartifacts.credentialprovider://auth";

    public static PublicClientApplicationBuilder CreateDefaultBuilder(Uri authority)
    {
        // Azure Artifacts is not yet present in PPE, so revert to the old app in that case
        bool prod = !authority.Host.Equals("login.windows-ppe.net", StringComparison.OrdinalIgnoreCase);

        var builder = PublicClientApplicationBuilder.Create(prod ? AzureArtifacts.ClientId : AzureArtifacts.LegacyClientId)
            .WithAuthority(authority);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            builder = builder.WithRedirectUri(MacOSXRedirectUri);
        }
        else
        {
            builder = builder.WithDefaultRedirectUri();
        }

        return builder;
    }

    public static PublicClientApplicationBuilder WithBroker(this PublicClientApplicationBuilder builder, bool enableBroker, IntPtr? parentWindowHandle, ILogger logger)
    {
        // Eventually will be rolled into CreateDefaultBuilder as using the brokers is desirable
        if (!enableBroker && (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)))
        {
            return builder;
        }

        logger.LogTrace("Using broker");

        return builder
            .WithBroker(
                new BrokerOptions(BrokerOptions.OperatingSystems.Windows | BrokerOptions.OperatingSystems.OSX | BrokerOptions.OperatingSystems.Linux)
                {
                    Title = "Azure DevOps Artifacts",
                    ListOperatingSystemAccounts = true,
                    MsaPassthrough = true
                })
            .WithParentActivityOrWindow(() => parentWindowHandle ?? GetConsoleOrTerminalWindow());
    }

    public static PublicClientApplicationBuilder WithBroker(this PublicClientApplicationBuilder builder, bool enableBroker, ILogger logger)
    {
        return builder.WithBroker(enableBroker, null, logger);
    }

    public static PublicClientApplicationBuilder WithHttpClient(this PublicClientApplicationBuilder builder, HttpClient? httpClient = null)
    {
        // Default HttpClient is only meant for .NET Framework clients that can't use the SocketsHttpHandler
        return builder.WithHttpClientFactory(new MsalHttpClientFactory(httpClient ?? new HttpClient(new HttpClientHandler
        {
            // Important for IWA
            UseDefaultCredentials = true
        })));
    }

#region https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/3590
    enum GetAncestorFlags
    {
        GetParent = 1,
        GetRoot = 2,
        /// <summary>
        /// Retrieves the owned root window by walking the chain of parent and owner windows returned by GetParent.
        /// </summary>
        GetRootOwner = 3
    }

    /// <summary>
    /// Retrieves the handle to the ancestor of the specified window.
    /// </summary>
    /// <param name="hwnd">A handle to the window whose ancestor is to be retrieved.
    /// If this parameter is the desktop window, the function returns NULL. </param>
    /// <param name="flags">The ancestor to be retrieved.</param>
    /// <returns>The return value is the handle to the ancestor window.</returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("libX11")]
    public static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11")]
    public static extern IntPtr XDefaultRootWindow(IntPtr display);


    private static IntPtr GetConsoleOrTerminalWindow()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IntPtr.Zero;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            IntPtr display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return XDefaultRootWindow(display);
        }

        IntPtr consoleHandle = GetConsoleWindow();
        IntPtr handle = GetAncestor(consoleHandle, GetAncestorFlags.GetRootOwner);

        return handle;
    }
#endregion
}
