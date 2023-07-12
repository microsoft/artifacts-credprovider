// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Microsoft.Artifacts.Authentication;

public static class MsalCache
{
    private static readonly string LocalAppDataLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);

    // from https://github.com/GitCredentialManager/git-credential-manager/blob/df90676d1249759eef8cec57155c27e869503225/src/shared/Microsoft.Git.CredentialManager/Authentication/MicrosoftAuthentication.cs#L277
    //      The Visual Studio MSAL cache is located at "%LocalAppData%\.IdentityService\msal.cache" on Windows.
    //      We use the MSAL extension library to provide us consistent cache file access semantics (synchronization, etc)
    //      as Visual Studio itself follows, as well as other Microsoft developer tools such as the Azure PowerShell CLI.
    public static string DefaultMsalCacheLocation
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // The shared MSAL cache is located at "%LocalAppData%\.IdentityService\msal.cache" on Windows.
                return Path.Combine(LocalAppDataLocation, ".IdentityService", "msal.cache");
            }
            else
            {
                // The shared MSAL cache metadata is located at "~/.local/.IdentityService/msal.cache" on UNIX.
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", ".IdentityService", "msal.cache");
            }
        }
    }

    public static async Task<MsalCacheHelper> GetMsalCacheHelperAsync(string cacheLocation, ILogger logger)
    {
        MsalCacheHelper? helper = null;

        logger.LogTrace(Resources.MsalCacheLocation, cacheLocation);

        var fileName = Path.GetFileName(cacheLocation);
        var directory = Path.GetDirectoryName(cacheLocation);

        // Copied from GCM https://github.com/GitCredentialManager/git-credential-manager/blob/bdc20d91d325d66647f2837ffb4e2b2fe98d7e70/src/shared/Core/Authentication/MicrosoftAuthentication.cs#L371-L407
        try
        {
            var storageProps = CreateTokenCacheProperties(useLinuxFallback: false);

            helper = await MsalCacheHelper.CreateAsync(storageProps);

            helper.VerifyPersistence();
        }
        catch (MsalCachePersistenceException ex)
        {
            logger.LogWarning(Resources.MsalCachePersistenceWarning);
            logger.LogTrace(ex.ToString());

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // On macOS sometimes the Keychain returns the "errSecAuthFailed" error - we don't know why
                // but it appears to be something to do with not being able to access the keychain.
                // Locking and unlocking (or restarting) often fixes this.
                logger.LogError(Resources.MsalCachePersistenceMacOsWarning);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // On Linux the SecretService/keyring might not be available so we must fall-back to a plaintext file.
                logger.LogWarning(Resources.MsalCacheUnprotectedFileWarning);

                var storageProps = CreateTokenCacheProperties(useLinuxFallback: true);
                helper = await MsalCacheHelper.CreateAsync(storageProps);
            }
        }

        StorageCreationProperties CreateTokenCacheProperties(bool useLinuxFallback)
        {
            var builder = new StorageCreationPropertiesBuilder(fileName, directory)
                .WithMacKeyChain("Microsoft.Developer.IdentityService", "MSALCache");

            if (useLinuxFallback)
            {
                builder.WithLinuxUnprotectedFile();
            }
            else
            {
                // The SecretService/keyring is used on Linux with the following collection name and attributes
                builder.WithLinuxKeyring(fileName,
                    "default", "MSALCache",
                    new KeyValuePair<string, string>("MsalClientID", "Microsoft.Developer.IdentityService"),
                    new KeyValuePair<string, string>("Microsoft.Developer.IdentityService", "1.0.0.0"));
            }

            return builder.Build();
        }

        return helper!;
    }
}
