using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace Microsoft.Artifacts.Authentication;

public static class AzureArtifacts
{
    public const string ClientId = "d5a56ea4-7369-46b8-a538-c370805301bf";

    public static PublicClientApplicationBuilder CreateDefaultBuilder(Uri authority, ILogger logger)
    {
        var builder = PublicClientApplicationBuilder.Create(AzureArtifacts.ClientId)
            .WithAuthority(authority)
            .WithRedirectUri("http://localhost")
            .WithLogging(
                (Identity.Client.LogLevel level, string message, bool _) =>
                {
                    // We ignore containsPii param because we are passing in enablePiiLogging below.
                    logger.LogTrace(Resources.MsalLogMessage, level, message);
                },
                enablePiiLogging: false
            );

        return builder;
    }

    public static PublicClientApplicationBuilder WithBroker(this PublicClientApplicationBuilder builder, bool enableBroker, ILogger logger)
    {
        // Eventually will be rolled into CreateDefaultBuilder as using the brokers is desirable
        if (!enableBroker)
        {
            return builder;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            logger.LogTrace(Resources.MsalUsingWamBroker);

            builder
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
                {
                    Title = "Azure DevOps Artifacts",
                    ListOperatingSystemAccounts = true,
                    MsaPassthrough = true
                })
                .WithParentActivityOrWindow(() => GetConsoleOrTerminalWindow());
        }
        else
        {
            logger.LogTrace(Resources.MsalUsingBroker);
            builder.WithBroker();
        }

        return builder;
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

    private static IntPtr GetConsoleOrTerminalWindow()
    {
        IntPtr consoleHandle = GetConsoleWindow();
        IntPtr handle = GetAncestor(consoleHandle, GetAncestorFlags.GetRootOwner);

        return handle;
    }
#endregion
}
