// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace NuGetCredentialProvider.Util
{
    internal static class PlatformUtils
    {
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
#if NETFRAMEWORK
            return ".NETFramework,Version=v4.6.1";
#else
            return AppContext.TargetFrameworkName;
#endif
        }

        public static string GetClrRuntime()
        {
#if NETFRAMEWORK
            return "win-x64";
#elif NETCOREAPP3_1
            return AppContext.GetData("RUNTIME_IDENTIFIER") as string ?? "unknown";
#else
            return RuntimeInformation.RuntimeIdentifier;
#endif
        }

        public static string GetClrDescription()
        {
            return RuntimeInformation.FrameworkDescription;
        }
    }
}
