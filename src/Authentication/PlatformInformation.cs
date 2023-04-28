// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Artifacts.Authentication;

public static class PlatformInformation
{
    private static string? programName = null;
    private static string? programVersion = null;
    private static string? runtimeIdentifier = null;

    private static AssemblyName CurrentAssemblyName => typeof(PlatformInformation).Assembly.GetName();

    public static string GetProgramName()
    {
        return programName ??= Assembly
            .GetEntryAssembly()?
            .GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? CurrentAssemblyName.Name;
    }

    public static string GetProgramVersion()
    {
        return programVersion ??= Assembly
            .GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? CurrentAssemblyName.Version.ToString();
    }

    public static string GetOSType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return nameof(OSPlatform.Windows);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return nameof(OSPlatform.Linux);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return nameof(OSPlatform.OSX);
        }

        return "Unknown";
    }

    public static string GetCpuArchitecture()
    {
        return RuntimeInformation.OSArchitecture.ToString();
    }

    public static string GetOsDescription()
    {
        return RuntimeInformation.OSDescription;
    }

    public static string GetClrVersion()
    {
        return Environment.Version.ToString();
    }

    public static string GetClrFramework()
    {
        return AppContext.TargetFrameworkName;
    }

    public static string GetClrRuntime()
    {
        // RuntimeInformation.RuntimeIdentifier not available on .NET Standard
        return runtimeIdentifier ??= AppContext.GetData("RUNTIME_IDENTIFIER") as string ?? "win-x64";
    }

    public static string GetClrDescription()
    {
        return RuntimeInformation.FrameworkDescription;
    }
}
