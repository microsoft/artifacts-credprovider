using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NuGetCredentialProvider.Util
{
    internal static class WindowsIntegratedAuthUtils
    {
        // Adapted from https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/blob/dev/core/src/Platforms/net45/NetDesktopPlatformProxy.cs
        [DllImport("secur32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool GetUserNameEx(int nameFormat, StringBuilder userName, ref uint userNameSize);

        public static bool SupportsWindowsIntegratedAuth()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        public static string GetUserPrincipalName()
        {
            try
            {
                const int NameUserPrincipal = 8;
                uint userNameSize = 0;
                GetUserNameEx(NameUserPrincipal, null, ref userNameSize);
                if (userNameSize == 0)
                {
                    return null;
                }

                StringBuilder sb = new StringBuilder((int)userNameSize);
                if (!GetUserNameEx(NameUserPrincipal, sb, ref userNameSize))
                {
                    return null;
                }

                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
