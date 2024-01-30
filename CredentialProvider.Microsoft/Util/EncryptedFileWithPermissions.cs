// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Win32.SafeHandles;

namespace NuGetCredentialProvider.Util
{
    public class EncryptedFileWithPermissions
    {
        #region Unix specific

        /// <summary>
        /// Equivalent to calling open() with flags  O_CREAT|O_WRONLY|O_TRUNC. O_TRUNC will truncate the file. 
        /// See https://man7.org/linux/man-pages/man2/open.2.html
        /// </summary>
        [DllImport("libc", EntryPoint = "creat", SetLastError = true)]
        private static extern int PosixCreate([MarshalAs(UnmanagedType.LPStr)] string pathname, int mode);

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int PosixChmod([MarshalAs(UnmanagedType.LPStr)] string pathname, int mode);

        #endregion

        public static byte[] ReadFileBytes(string filePath, bool readUnencrypted = false)
        {
            try
            {
                return File.Exists(filePath) ? ProtectedData.Unprotect(File.ReadAllBytes(filePath), null, DataProtectionScope.CurrentUser) : null;
            }
            catch (NotSupportedException)
            {
                if (readUnencrypted)
                {
                    return File.Exists(filePath) ? File.ReadAllBytes(filePath) : null;
                }

                throw;
            }
        }

        public static void WriteFileBytes(string filePath, byte[] bytes, bool writeUnencrypted = false)
        {
            try
            {
                EnsureDirectoryExists(filePath);
                
                WriteToNewFileWithOwnerRWPermissions(filePath, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
            }
            catch (NotSupportedException)
            {
                if (writeUnencrypted)
                {
                    WriteToNewFileWithOwnerRWPermissions(filePath, bytes);
                    return;
                }

                throw;
            }
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Based on https://stackoverflow.com/questions/45132081/file-permissions-on-linux-unix-with-net-core and on 
        /// https://github.com/NuGet/NuGet.Client/commit/d62db666c710bf95121fe8f5c6a6cbe01985456f and 
        /// https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/b299b2581da87af50fde751e689f1bd4114516ce/src/client/Microsoft.Identity.Client.Extensions.Msal/Accessors/FileWithPermissions.cs
        /// </summary>
        private static void WriteToNewFileWithOwnerRWPermissions(string path, byte[] bytes)
        {

            if (SharedUtilities.IsWindowsPlatform())
            {
                WriteToNewFileWithOwnerRWPermissionsWindows(path, bytes);
            }
            else if (SharedUtilities.IsMacPlatform() || SharedUtilities.IsLinuxPlatform())
            {
                WriteToNewFileWithOwnerRWPermissionsUnix(path, bytes);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        private static void WriteToNewFileWithOwnerRWPermissionsUnix(string path, byte[] bytes)
        {
            int _0600 = Convert.ToInt32("600", 8);

            int fileDescriptor = PosixCreate(path, _0600);

            // if creat() fails, then try to use File.Create because it will throw a meaningful exception.
            if (fileDescriptor == -1)
            {
                int posixCreateError = Marshal.GetLastWin32Error();
                using (File.Create(path))
                {
                    // File.Create() should have thrown an exception with an appropriate error message
                }
                File.Delete(path);
                throw new InvalidOperationException($"libc creat() failed with last error code {posixCreateError}, but File.Create did not");
            }

            var safeFileHandle = new SafeFileHandle((IntPtr)fileDescriptor, ownsHandle: true);
            using var fileStream = new FileStream(safeFileHandle, FileAccess.ReadWrite);
            fileStream.Write(bytes, 0, bytes.Length);
        }

#pragma warning disable CA1416 // Validate platform compatibility
        private static void WriteToNewFileWithOwnerRWPermissionsWindows(string filePath, byte[] bytes)
        {
            FileSecurity security = new();

            var rights = FileSystemRights.Read | FileSystemRights.Write;

            security.AddAccessRule(
                new FileSystemAccessRule(
                        WindowsIdentity.GetCurrent().Name,
                        rights,
                        InheritanceFlags.None,
                        PropagationFlags.NoPropagateInherit,
                        AccessControlType.Allow));

            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            FileStream fs = null;

            try
            {
#if NET45_OR_GREATER
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                fs = File.Create(filePath, bytes.Length, FileOptions.None, security);
#else
                FileInfo info = new FileInfo(filePath);
                fs = info.Create(FileMode.Create, rights, FileShare.Read, bytes.Length, FileOptions.None, security);
#endif

                fs.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                fs?.Dispose();
            }
        }
#pragma warning restore CA1416 // Validate platform compatibility
    }
}
