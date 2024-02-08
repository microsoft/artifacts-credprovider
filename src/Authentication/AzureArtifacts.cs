// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
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

    public static PublicClientApplicationBuilder CreateDefaultBuilder(Uri authority)
    {
        // Azure Artifacts is not yet present in PPE, so revert to the old app in that case
        bool prod = !authority.Host.Equals("login.windows-ppe.net", StringComparison.OrdinalIgnoreCase);

        var builder = PublicClientApplicationBuilder.Create(prod ? AzureArtifacts.ClientId : AzureArtifacts.LegacyClientId)
            .WithAuthority(authority)
            .WithRedirectUri("http://localhost");

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

    private static IntPtr GetConsoleOrTerminalWindow()
    {
        IntPtr consoleHandle = GetConsoleWindow();
        IntPtr handle = GetAncestor(consoleHandle, GetAncestorFlags.GetRootOwner);

		if (handle == IntPtr.Zero)
		{
			// Cannot get handle to console window, walk up parent processes until we find one that has a MainWindowHandle
			var parent = ParentProcessUtilities.GetParentProcess();
			while (parent != null)
			{
				if (parent.MainWindowHandle != IntPtr.Zero)
				{
					handle = parent.MainWindowHandle;
					break;
				}
				parent = ParentProcessUtilities.GetParentProcess(parent.Handle);
			}
		}

		return handle;
    }

	/// <summary>
	/// A utility class to determine a process parent.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	private struct ParentProcessUtilities
	{
		// These members must match PROCESS_BASIC_INFORMATION
		internal IntPtr Reserved1;
		internal IntPtr PebBaseAddress;
		internal IntPtr Reserved2_0;
		internal IntPtr Reserved2_1;
		internal IntPtr UniqueProcessId;
		internal IntPtr InheritedFromUniqueProcessId;

		[DllImport("ntdll.dll")]
		private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);

		/// <summary>
		/// Gets the parent process of the current process.
		/// </summary>
		/// <returns>An instance of the Process class.</returns>
		public static Process? GetParentProcess()
		{
			return GetParentProcess(Process.GetCurrentProcess().Handle);
		}

		/// <summary>
		/// Gets the parent process of specified process.
		/// </summary>
		/// <param name="id">The process id.</param>
		/// <returns>An instance of the Process class.</returns>
		public static Process? GetParentProcess(int id)
		{
			Process process = Process.GetProcessById(id);
			return GetParentProcess(process.Handle);
		}

		/// <summary>
		/// Gets the parent process of a specified process.
		/// </summary>
		/// <param name="handle">The process handle.</param>
		/// <returns>An instance of the Process class.</returns>
		public static Process? GetParentProcess(IntPtr handle)
		{
			ParentProcessUtilities pbi = new ParentProcessUtilities();
			int returnLength;
			int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
			if (status != 0)
				throw new Win32Exception(status);

			try
			{
				return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
			}
			catch (ArgumentException)
			{
				// not found
				return null;
			}
		}
	}
	#endregion
}
