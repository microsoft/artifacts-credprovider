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
        
        public const string LogPathEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_LOG_PATH";
        public const string LegacyLogPathEnvVar = "NUGET_CREDENTIALPROVIDER_LOG_PATH";
        public const string LogPIIEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_LOG_PII";
        public const string LegacyLogPIIEnvVar = "NUGET_CREDENTIALPROVIDER_LOG_PII";
        public const string SessionTokenCacheEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED";
        public const string LegacySessionTokenCacheEnvVar = "NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED";
        public const string WindowsIntegratedAuthenticationEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED";
        public const string LegacyWindowsIntegratedAuthenticationEnvVar = "NUGET_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED";
        public const string ForceCanShowDialogEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO";
        public const string LegacyForceCanShowDialogEnvVar = "NUGET_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO";
        public const string DeviceFlowTimeoutEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_DEVICEFLOWTIMEOUTSECONDS";
        public const string LegacyDeviceFlowTimeoutEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_DEVICEFLOWTIMEOUTSECONDS";
        public const string SupportedHostsEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_VSTS_HOSTS";
        public const string LegacySupportedHostsEnvVar = "NUGET_CREDENTIALPROVIDER_HOSTS";
        public const string PpeHostsEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_PPEHOSTS";
        public const string LegacyPpeHostsEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_PPEHOSTS";
        public const string SessionTimeEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_VSTS_SESSIONTIMEMINUTES";
        public const string LegacySessionTimeEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_SESSIONTIMEMINUTES";
        public const string TokenTypeEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_VSTS_TOKENTYPE";
        public const string LegacyTokenTypeEnvVar = "NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE";
        public const string BuildTaskUriPrefixes = "ARTIFACTS_CREDENTIALPROVIDER_URI_PREFIXES";
        public const string LegacyBuildTaskUriPrefixes = "VSS_NUGET_URI_PREFIXES";
        public const string BuildTaskAccessToken = "ARTIFACTS_CREDENTIALPROVIDER_ACCESSTOKEN";
        public const string LegacyBuildTaskAccessToken = "VSS_NUGET_ACCESSTOKEN";
        public const string MsalLoginHintEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_MSAL_LOGIN_HINT";
        public const string LegacyMsalLoginHintEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_LOGIN_HINT";
        public const string MsalAuthorityEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_MSAL_AUTHORITY";
        public const string LegacyMsalAuthorityEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_AUTHORITY";
        public const string MsalFileCacheEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED";
        public const string LegacyMsalFileCacheEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED";
        public const string MsalFileCacheLocationEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION";
        public const string LegacyMsalFileCacheLocationEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION";
        public const string MsalAllowBrokerEnvVar = "ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER";
        public const string LegacyMsalAllowBrokerEnvVar = "NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER";
        public const string EndpointCredentials = "ARTIFACTS_CREDENTIALPROVIDER_FEED_ENDPOINTS";
        public const string BuildTaskExternalEndpoints = "ARTIFACTS_CREDENTIALPROVIDER_EXTERNAL_FEED_ENDPOINTS";
        public const string LegacyBuildTaskExternalEndpoints = "VSS_NUGET_EXTERNAL_FEED_ENDPOINTS";
        public const string ProgramContext = "ARTIFACTS_CREDENTIALPROVIDER_PROGRAM_CONTEXT";

        // Map of new environment variables to their legacy equivalents
        private static readonly Dictionary<string, string> EnvVarLegacyMap = new Dictionary<string, string>
        {
            { LogPathEnvVar, LegacyLogPathEnvVar },
            { LogPIIEnvVar, LegacyLogPIIEnvVar },
            { SessionTokenCacheEnvVar, LegacySessionTokenCacheEnvVar },
            { WindowsIntegratedAuthenticationEnvVar, LegacyWindowsIntegratedAuthenticationEnvVar },
            { ForceCanShowDialogEnvVar, LegacyForceCanShowDialogEnvVar },
            { DeviceFlowTimeoutEnvVar, LegacyDeviceFlowTimeoutEnvVar },
            { SupportedHostsEnvVar, LegacySupportedHostsEnvVar },
            { PpeHostsEnvVar, LegacyPpeHostsEnvVar },
            { SessionTimeEnvVar, LegacySessionTimeEnvVar },
            { TokenTypeEnvVar, LegacyTokenTypeEnvVar },
            { BuildTaskUriPrefixes, LegacyBuildTaskUriPrefixes },
            { BuildTaskAccessToken, LegacyBuildTaskAccessToken },
            { MsalLoginHintEnvVar, LegacyMsalLoginHintEnvVar },
            { MsalAuthorityEnvVar, LegacyMsalAuthorityEnvVar },
            { MsalFileCacheEnvVar, LegacyMsalFileCacheEnvVar },
            { MsalFileCacheLocationEnvVar, LegacyMsalFileCacheLocationEnvVar },
            { MsalAllowBrokerEnvVar, LegacyMsalAllowBrokerEnvVar },
            { BuildTaskExternalEndpoints, LegacyBuildTaskExternalEndpoints },
        };

        // Prefer new variable, if null fallback to legacy
        public static string GetEnvironmentVariable(string envVar)
        {
            var val = EnvUtil.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(val)) return val;
            if (EnvVarLegacyMap.TryGetValue(envVar, out var legacyVar))
            {
                return EnvUtil.GetEnvironmentVariable(legacyVar);
            }
            return null;
        }

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

        public static string FileLogLocation => GetEnvironmentVariable(LogPathEnvVar);

        public static string SessionTokenCacheLocation { get; } = Path.Combine(LocalAppDataLocation, CredentialProviderFolderName, "SessionTokenCache.dat");

        public static Uri GetAuthorityFromEnvironment(ILogger logger)
        {
            var environmentAuthority = GetEnvironmentVariable(MsalAuthorityEnvVar);
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
            return GetEnvironmentVariable(MsalLoginHintEnvVar);
        }

        public static string GetMsalCacheLocation()
        {
            var msalCacheFromEnvironment = GetEnvironmentVariable(MsalFileCacheLocationEnvVar);
            return string.IsNullOrWhiteSpace(msalCacheFromEnvironment) ? DefaultMsalCacheLocation : msalCacheFromEnvironment;
        }

        public static bool MsalFileCacheEnabled()
        {
            return GetEnabledFromEnvironment(MsalFileCacheEnvVar, true);
        }

        public static bool MsalAllowBrokerEnabled()
        {
            return GetEnabledFromEnvironment(MsalAllowBrokerEnvVar, RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        }

        public static IList<string> GetHostsFromEnvironment(ILogger logger, string envVar, IEnumerable<string> defaultHosts, [CallerMemberName] string collectionName = null)
        {
            var hosts = new List<string>();
            var hostsFromEnvironment = GetEnvironmentVariable(envVar)?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
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
            var fromEnv = GetEnvironmentVariable(ForceCanShowDialogEnvVar);
            if (string.IsNullOrWhiteSpace(fromEnv) || !bool.TryParse(fromEnv, out var parsed))
                return default;
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
            var minutes = GetEnvironmentVariable(SessionTimeEnvVar);
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
            var timeout = GetEnvironmentVariable(DeviceFlowTimeoutEnvVar);
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
            if (Enum.TryParse<VstsTokenType>(GetEnvironmentVariable(TokenTypeEnvVar), ignoreCase: true, out VstsTokenType result))
            {
                return result;
            }
            return null;
        }

        public static Context? GetProgramContextFromEnvironment()
        {
            var context = EnvUtil.GetEnvironmentVariable(ProgramContext);
            if (!string.IsNullOrWhiteSpace(context) && Enum.TryParse<Context>(context, ignoreCase: true, out Context result))
            {
                return result;
            }
            return null;
        }

        public static void SetProgramContextInEnvironment(Context context)
        {
            Environment.SetEnvironmentVariable(ProgramContext, context.ToString());
        }

        private static bool GetEnabledFromEnvironment(string artifactsVar, bool defaultValue = true)
        {
            var val = GetEnvironmentVariable(artifactsVar);
            if (bool.TryParse(val, out bool result))
                return result;
            return defaultValue;
        }
    }
}
