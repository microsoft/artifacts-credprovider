using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;

namespace NuGetCredentialProvider.Tests.Util
{
    [TestClass]
    public class EnvUtilTests
    {
        private Mock<ILogger> loggerMock;
        [TestInitialize]
        public void TestInitialize()
        {
            loggerMock = new Mock<ILogger>();
            // Clear all relevant environment variables before each test
            foreach (var envVar in new[] {
                EnvUtil.LogPathEnvVar, EnvUtil.LegacyLogPathEnvVar,
                EnvUtil.LogPIIEnvVar, EnvUtil.LegacyLogPIIEnvVar,
                EnvUtil.SessionTokenCacheEnvVar, EnvUtil.LegacySessionTokenCacheEnvVar,
                EnvUtil.WindowsIntegratedAuthenticationEnvVar, EnvUtil.LegacyWindowsIntegratedAuthenticationEnvVar,
                EnvUtil.ForceCanShowDialogEnvVar, EnvUtil.LegacyForceCanShowDialogEnvVar,
                EnvUtil.DeviceFlowTimeoutEnvVar, EnvUtil.LegacyDeviceFlowTimeoutEnvVar,
                EnvUtil.SupportedHostsEnvVar, EnvUtil.LegacySupportedHostsEnvVar,
                EnvUtil.PpeHostsEnvVar, EnvUtil.LegacyPpeHostsEnvVar,
                EnvUtil.SessionTimeEnvVar, EnvUtil.LegacySessionTimeEnvVar,
                EnvUtil.TokenTypeEnvVar, EnvUtil.LegacyTokenTypeEnvVar,
                EnvUtil.BuildTaskUriPrefixes, EnvUtil.LegacyBuildTaskUriPrefixes,
                EnvUtil.BuildTaskAccessToken, EnvUtil.LegacyBuildTaskAccessToken,
                EnvUtil.MsalLoginHintEnvVar, EnvUtil.LegacyMsalLoginHintEnvVar,
                EnvUtil.MsalAuthorityEnvVar, EnvUtil.LegacyMsalAuthorityEnvVar,
                EnvUtil.MsalFileCacheEnvVar, EnvUtil.LegacyMsalFileCacheEnvVar,
                EnvUtil.MsalFileCacheLocationEnvVar, EnvUtil.LegacyMsalFileCacheLocationEnvVar,
                EnvUtil.MsalAllowBrokerEnvVar, EnvUtil.LegacyMsalAllowBrokerEnvVar,
                EnvUtil.EndpointCredentials, EnvUtil.BuildTaskExternalEndpoints, EnvUtil.LegacyBuildTaskExternalEndpoints,
                EnvUtil.ProgramContext
            })
            {
                Environment.SetEnvironmentVariable(envVar, null);
            }
        }

        [DataTestMethod]
        [DataRow("NUGET_CREDENTIALPROVIDER_LOG_PATH", "ARTIFACTS_CREDENTIALPROVIDER_LOG_PATH", "oldPath", "newpath")]
        [DataRow("NUGET_CREDENTIALPROVIDER_LOG_PII", "ARTIFACTS_CREDENTIALPROVIDER_LOG_PII", "oldPII", "newPII")]
        [DataRow("NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED", "ARTIFACTS_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED", "oldCache", "newCache")]
        [DataRow("NUGET_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED", "ARTIFACTS_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED", "oldWinAuth", "newWinAuth")]
        [DataRow("NUGET_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO", "ARTIFACTS_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO", "oldDialog", "newDialog")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_DEVICEFLOWTIMEOUTSECONDS", "ARTIFACTS_CREDENTIALPROVIDER_DEVICEFLOWTIMEOUTSECONDS", "oldTimeout", "newTimeout")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_HOSTS", "ARTIFACTS_CREDENTIALPROVIDER_HOSTS", "oldHosts", "newHosts")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_PPEHOSTS", "ARTIFACTS_CREDENTIALPROVIDER_PPEHOSTS", "oldPPE", "newPPE")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_SESSIONTIMEMINUTES", "ARTIFACTS_CREDENTIALPROVIDER_SESSIONTIMEMINUTES", "oldSession", "newSession")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE", "ARTIFACTS_CREDENTIALPROVIDER_TOKENTYPE", "oldToken", "newToken")]
        [DataRow("VSS_NUGET_URI_PREFIXES", "ARTIFACTS_CREDENTIALPROVIDER_URI_PREFIXES", "oldUri", "newUri")]
        [DataRow("VSS_NUGET_ACCESSTOKEN", "ARTIFACTS_CREDENTIALPROVIDER_ACCESSTOKEN", "oldToken", "newToken")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_LOGIN_HINT", "ARTIFACTS_CREDENTIALPROVIDER_MSAL_LOGIN_HINT", "oldHint", "newHint")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_AUTHORITY", "ARTIFACTS_CREDENTIALPROVIDER_MSAL_AUTHORITY", "oldAuth", "newAuth")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED", "ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED", "oldCache", "newCache")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION", "ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION", "oldLoc", "newLoc")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER", "ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER", "oldBroker", "newBroker")]
        [DataRow("VSS_NUGET_EXTERNAL_FEED_ENDPOINTS", "ARTIFACTS_CREDENTIALPROVIDER_EXTERNAL_FEED_ENDPOINTS", "oldExtFeed", "newExtFeed")]
        public void GetPreferredOrLegancyEnvVar_PrefersNewOverOld(string oldVar, string newVar, string oldValue, string newValue)
        {
            Environment.SetEnvironmentVariable(oldVar, oldValue);
            Environment.SetEnvironmentVariable(newVar, newValue);
            Assert.AreEqual(newValue, EnvUtil.GetEnvironmentVariable(newVar));
        }

        [DataTestMethod]
        [DataRow("NUGET_CREDENTIALPROVIDER_LOG_PATH", "legacyPath")]
        [DataRow("NUGET_CREDENTIALPROVIDER_LOG_PII", "legacyPII")]
        [DataRow("NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED", "legacyCache")]
        [DataRow("NUGET_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED", "legacyWinAuth")]
        [DataRow("NUGET_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO", "legacyDialog")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_DEVICEFLOWTIMEOUTSECONDS", "legacyTimeout")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_HOSTS", "legacyHosts")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_PPEHOSTS", "legacyPPE")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_SESSIONTIMEMINUTES", "legacySession")]
        [DataRow("NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE", "legacyToken")]
        [DataRow("VSS_NUGET_URI_PREFIXES", "legacyUri")]
        [DataRow("VSS_NUGET_ACCESSTOKEN", "legacyToken")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_LOGIN_HINT", "legacyHint")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_AUTHORITY", "legacyAuth")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED", "legacyCache")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION", "legacyLoc")]
        [DataRow("NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER", "legacyBroker")]
        [DataRow("VSS_NUGET_EXTERNAL_FEED_ENDPOINTS", "legacyExtFeed")]
        public void GetPreferredOrLegancyEnvVar_FallsBackToLegacyWhenNewEnvironmentVariableIsNotAvailable(string oldVar, string value)
        {
            Environment.SetEnvironmentVariable(oldVar, value);
            Assert.AreEqual(value, EnvUtil.GetEnvironmentVariable(oldVar));
        }

        [TestMethod]
        public void GetLogPIIEnabled_ReturnsCorrectValue()
        {
            Environment.SetEnvironmentVariable(EnvUtil.LogPIIEnvVar, "true");
            Assert.IsTrue(EnvUtil.GetLogPIIEnabled());
        }
           public void GetLogPIIEnabled_ReturnsDefaultVIfUnset()
        {
            Assert.IsFalse(EnvUtil.GetLogPIIEnabled());
        }


        [TestMethod]
        public void FileLogLocation_ReturnsCorrectValue()
        {
            Environment.SetEnvironmentVariable(EnvUtil.LogPathEnvVar, "logfile");
            Assert.AreEqual("logfile", EnvUtil.FileLogLocation);
        }

        [TestMethod]
        public void GetMsalLoginHint_ReturnsCorrectValue()
        {
            Environment.SetEnvironmentVariable(EnvUtil.MsalLoginHintEnvVar, "hint");
            Assert.AreEqual("hint", EnvUtil.GetMsalLoginHint());
        }

        [TestMethod]
        public void GetMsalCacheLocation_ReturnsDefaultIfUnset()
        {
            Environment.SetEnvironmentVariable(EnvUtil.MsalFileCacheLocationEnvVar, null);
            Assert.AreEqual(EnvUtil.DefaultMsalCacheLocation, EnvUtil.GetMsalCacheLocation());
        }

        [TestMethod]
        public void GetMsalCacheLocation_ReturnsEnvVar()
        {
            Environment.SetEnvironmentVariable(EnvUtil.MsalFileCacheLocationEnvVar, "customcache");
            Assert.AreEqual("customcache", EnvUtil.GetMsalCacheLocation());
        }

        [TestMethod]
        public void MsalFileCacheEnabled_ParsesBool()
        {
            Environment.SetEnvironmentVariable(EnvUtil.MsalFileCacheEnvVar, "false");
            Assert.IsFalse(EnvUtil.MsalFileCacheEnabled());
            Environment.SetEnvironmentVariable(EnvUtil.MsalFileCacheEnvVar, "true");
            Assert.IsTrue(EnvUtil.MsalFileCacheEnabled());
        }

        [TestMethod]
        public void MsalAllowBrokerEnabled_ParsesBool()
        {
            Environment.SetEnvironmentVariable(EnvUtil.MsalAllowBrokerEnvVar, "true");
            Assert.IsTrue(EnvUtil.MsalAllowBrokerEnabled());
            Environment.SetEnvironmentVariable(EnvUtil.MsalAllowBrokerEnvVar, "false");
            Assert.IsFalse(EnvUtil.MsalAllowBrokerEnabled());
        }

        [TestMethod]
        public void GetSessionTimeFromEnvironment_ParsesMinutes()
        {
            Environment.SetEnvironmentVariable(EnvUtil.SessionTimeEnvVar, "15");
            Assert.AreEqual(TimeSpan.FromMinutes(15), EnvUtil.GetSessionTimeFromEnvironment(loggerMock.Object));
        }

        [TestMethod]
        public void GetDeviceFlowTimeoutFromEnvironmentInSeconds_ParsesInt()
        {
            Environment.SetEnvironmentVariable(EnvUtil.DeviceFlowTimeoutEnvVar, "120");
            Assert.AreEqual(120, EnvUtil.GetDeviceFlowTimeoutFromEnvironmentInSeconds(loggerMock.Object));
        }

        [TestMethod]
        public void GetVstsTokenType_ParsesEnum()
        {
            Environment.SetEnvironmentVariable(EnvUtil.TokenTypeEnvVar, "SelfDescribing");
            Assert.AreEqual(VstsTokenType.SelfDescribing, EnvUtil.GetVstsTokenType());
        }

        [TestMethod]
        public void GetProgramContextFromEnvironment_ParsesEnum()
        {
            Environment.SetEnvironmentVariable(EnvUtil.ProgramContext, "NuGet");
            Assert.AreEqual(Context.NuGet, EnvUtil.GetProgramContextFromEnvironment());
        }

        [TestMethod]
        public void SetProgramContextInEnvironment_SetsValue()
        {
            EnvUtil.SetProgramContextInEnvironment(Context.NuGet);
            Assert.AreEqual("NuGet", EnvUtil.GetEnvironmentVariable(EnvUtil.ProgramContext));
        }
    }
}
