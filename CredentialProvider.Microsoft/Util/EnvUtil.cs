// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Artifacts.Authentication;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Logging;

namespace NuGetCredentialProvider.Util
{
    public static class EnvUtil
    {
        public const string LogPathEnvVar = "NUGET_CREDENTIALPROVIDER_LOG_PATH";
        public const string LogPIIEnvVar = "NUGET_CREDENTIALPROVIDER_LOG_PII";
        public const string SessionTokenCacheEnvVar = "NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED";
        public const string WindowsIntegratedAuthenticationEnvVar = "NUGET_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED";
        public const string ForceCanShowDialogEnvVar = "NUGET_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO";

        public const string DeviceFlowTimeoutEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_DEVICEFLOWTIMEOUTSECONDS";
        public const string SupportedHostsEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_HOSTS";
        public const string PpeHostsEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_PPEHOSTS";
        public const string SessionTimeEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_SESSIONTIMEMINUTES";
        public const string TokenTypeEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE";

        public const string BuildTaskUriPrefixes = "VSS_NUGET_URI_PREFIXES";
        public const string BuildTaskAccessToken = "VSS_NUGET_ACCESSTOKEN";
        public const string BuildTaskExternalEndpoints = "VSS_NUGET_EXTERNAL_FEED_ENDPOINTS";

        public const string MsalLoginHintEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_LOGIN_HINT";
        public const string MsalAuthorityEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_AUTHORITY";
        public const string MsalFileCacheEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED";
        public const string MsalFileCacheLocationEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION";
        public const string MsalAllowBrokerEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER";

        public static bool GetLogPIIEnabled()
        {
            return GetEnabledFromEnvironment(LogPIIEnvVar, defaultValue: false);
        }

        private static readonly string LocalAppDataLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);

        private const string CredentialProviderFolderName = "MicrosoftCredentialProvider";

        // from https://github.com/GitCredentialManager/git-credential-manager/blob/df90676d1249759eef8cec57155c27e869503225/src/shared/Microsoft.Git.CredentialManager/Authentication/MicrosoftAuthentication.cs#L277
        //      The Visual Studio MSAL cache is located at "%LocalAppData%\.IdentityService\msal.cache" on Windows.
        //      We use the MSAL extension library to provide us consistent cache file access semantics (synchronization, etc)
        //      as Visual Studio itself follows, as well as other Microsoft developer tools such as the Azure PowerShell CLI.
        public static string DefaultMsalCacheLocation => MsalCache.DefaultMsalCacheLocation;

        public static string FileLogLocation { get; } = Environment.GetEnvironmentVariable(LogPathEnvVar);

        public static string SessionTokenCacheLocation { get; } = Path.Combine(LocalAppDataLocation, CredentialProviderFolderName, "SessionTokenCache.dat");

        public static Uri GetAuthorityFromEnvironment(ILogger logger)
        {
            var environmentAuthority = Environment.GetEnvironmentVariable(MsalAuthorityEnvVar);
            if (environmentAuthority == null)
            {
                return null;
            }

            if (Uri.TryCreate(environmentAuthority, UriKind.Absolute, out Uri parsedUri))
            {
                logger.Verbose(string.Format(Resources.AADAuthorityOverrideFound, parsedUri.ToString()));
                return parsedUri;
            }
            else
            {
                logger.Warning(string.Format(Resources.CouldNotParseAADAuthorityOverride, environmentAuthority));
            }

            return null;
        }

        public static string GetMsalLoginHint()
        {
            return Environment.GetEnvironmentVariable(MsalLoginHintEnvVar);
        }

        public static string GetMsalCacheLocation()
        {
            string msalCacheFromEnvironment = Environment.GetEnvironmentVariable(MsalFileCacheLocationEnvVar);
            return string.IsNullOrWhiteSpace(msalCacheFromEnvironment) ? DefaultMsalCacheLocation : msalCacheFromEnvironment;
        }

        public static bool MsalFileCacheEnabled()
        {
            return GetEnabledFromEnvironment(MsalFileCacheEnvVar, defaultValue: true);
        }

        public static bool MsalAllowBrokerEnabled()
        {
            return GetEnabledFromEnvironment(MsalAllowBrokerEnvVar, defaultValue: RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        }

        public static IList<string> GetHostsFromEnvironment(ILogger logger, string envVar, IEnumerable<string> defaultHosts, [CallerMemberName] string collectionName = null)
        {
            var hosts = new List<string>();

            var hostsFromEnvironment = Environment.GetEnvironmentVariable(envVar)?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (hostsFromEnvironment != null)
            {
                logger.Verbose(string.Format(Resources.FoundHostsInEnvironment, collectionName));
                logger.Verbose(string.Join(",", hostsFromEnvironment));
                hosts.AddRange(hostsFromEnvironment);
            }

            hosts.AddRange(defaultHosts);
            return hosts;
        }

        public static bool? ForceCanShowDialogTo()
        {
            var fromEnv = Environment.GetEnvironmentVariable(ForceCanShowDialogEnvVar);
            if(string.IsNullOrWhiteSpace(fromEnv) || !bool.TryParse(fromEnv, out var parsed))
            {
                return default;
            }

            return parsed;
        }

        public static bool SessionTokenCacheEnabled()
        {
            return GetEnabledFromEnvironment(SessionTokenCacheEnvVar);
        }

        public static bool WindowsIntegratedAuthenticationEnabled()
        {
            return GetEnabledFromEnvironment(WindowsIntegratedAuthenticationEnvVar);
        }

        public static TimeSpan? GetSessionTimeFromEnvironment(ILogger logger)
        {
            var minutes = Environment.GetEnvironmentVariable(SessionTimeEnvVar);
            if (minutes != null)
            {
                if (double.TryParse(minutes, out double parsedMinutes))
                {
                    return TimeSpan.FromMinutes(parsedMinutes);
                }

                logger.Warning(string.Format(Resources.CouldNotParseSessionTimeOverride, minutes));
            }

            return null;
        }

        public static int GetDeviceFlowTimeoutFromEnvironmentInSeconds(ILogger logger)
        {
            var timeout = Environment.GetEnvironmentVariable(DeviceFlowTimeoutEnvVar);
            const int defaultTimeout = 90;
            if (timeout == null)
            {
                return defaultTimeout;
            }

            if (int.TryParse(timeout, out int parsedTimeout))
            {
                return parsedTimeout;
            }

            logger.Warning(string.Format(Resources.CouldNotParseDeviceFlowTimeoutOverride, timeout));

            return defaultTimeout;
        }

        public static VstsTokenType? GetVstsTokenType()
        {
            if (Enum.TryParse<VstsTokenType>(Environment.GetEnvironmentVariable(TokenTypeEnvVar), ignoreCase: true, out VstsTokenType result))
            {
                return result;
            }

            return null;
        }

        private static bool GetEnabledFromEnvironment(string envVar, bool defaultValue = true)
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable(envVar), out bool result))
            {
                return result;
            }

            return defaultValue;
        }
    }
}
