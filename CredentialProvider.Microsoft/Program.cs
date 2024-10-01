﻿// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Artifacts.Authentication;
using NuGet.Common;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.CredentialProviders;
using NuGetCredentialProvider.CredentialProviders.Vsts;
using NuGetCredentialProvider.CredentialProviders.VstsBuildTask;
using NuGetCredentialProvider.CredentialProviders.VstsBuildTaskServiceEndpoint;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.RequestHandlers;
using NuGetCredentialProvider.Util;
using PowerArgs;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider
{
    public static class Program
    {
        private static bool shuttingDown = false;
        public static bool IsShuttingDown => Volatile.Read(ref shuttingDown);

        public static async Task<int> Main(string[] args)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            var parsedArgs = await Args.ParseAsync<CredentialProviderArgs>(args);

            var multiLogger = new MultiLogger();
            var fileLogger = GetFileLogger();
            if (fileLogger != null)
            {
                fileLogger.Log(LogLevel.Verbose, allowOnConsole: false, string.Join(" ", MsalHttpClientFactory.UserAgent));

                multiLogger.Add(fileLogger);
            }

            // Cancellation listener
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs eventArgs) =>
            {
                // ConsoleCancelEventArgs.Cancel defaults to false which terminates the current process.
                multiLogger.Verbose(Resources.CancelMessage);
                tokenSource.Cancel();
            };

            var authUtil = new AuthUtil(multiLogger);
            var logger = new NuGetLoggerAdapter(multiLogger, parsedArgs.Verbosity);
            var tokenProvidersFactory = new MsalTokenProvidersFactory(logger);
            var vstsBuildTaskTokenProvidersFactory = new VstsBuildTaskMsalTokenProvidersFactory(logger);
            var vstsSessionTokenProvider = new VstsSessionTokenFromBearerTokenProvider(authUtil, multiLogger);

            List<ICredentialProvider> credentialProviders = new List<ICredentialProvider>
            {
                new VstsBuildTaskServiceEndpointCredentialProvider(multiLogger, vstsBuildTaskTokenProvidersFactory, authUtil),
                new VstsBuildTaskCredentialProvider(multiLogger),
                new VstsCredentialProvider(multiLogger, authUtil, tokenProvidersFactory, vstsSessionTokenProvider),
            };

            try
            {
                IRequestHandlers requestHandlers = new RequestHandlerCollection
                {
                    { MessageMethod.GetAuthenticationCredentials, new GetAuthenticationCredentialsRequestHandler(multiLogger, credentialProviders, tokenSource.Token) },
                    { MessageMethod.GetOperationClaims, new GetOperationClaimsRequestHandler(multiLogger, credentialProviders) },
                    { MessageMethod.Initialize, new InitializeRequestHandler(multiLogger) },
                    { MessageMethod.SetLogLevel, new SetLogLevelRequestHandler(multiLogger) },
                    { MessageMethod.SetCredentials, new SetCredentialsRequestHandler(multiLogger) },
                };

                // Help
                if (parsedArgs.Help)
                {
                    Console.WriteLine(string.Format(Resources.CommandLineArgs, PlatformInformation.GetProgramVersion(), Environment.CommandLine));
                    Console.WriteLine(ArgUsage.GenerateUsageFromTemplate<CredentialProviderArgs>());
                    Console.WriteLine(
                        string.Format(
                            Resources.EnvironmentVariableHelp,
                            EnvUtil.LogPathEnvVar,
                            EnvUtil.SessionTokenCacheEnvVar,
                            EnvUtil.SupportedHostsEnvVar,
                            EnvUtil.SessionTimeEnvVar,
                            EnvUtil.TokenTypeEnvVar,
                            EnvUtil.BuildTaskUriPrefixes,
                            EnvUtil.BuildTaskAccessToken,
                            EnvUtil.BuildTaskExternalEndpoints,
                            EnvUtil.EndpointCredentials,
                            EnvUtil.DefaultMsalCacheLocation,
                            EnvUtil.SessionTokenCacheLocation,
                            EnvUtil.WindowsIntegratedAuthenticationEnvVar,
                            EnvUtil.DeviceFlowTimeoutEnvVar,
                            EnvUtil.ForceCanShowDialogEnvVar,
                            EnvUtil.MsalAuthorityEnvVar,
                            EnvUtil.MsalFileCacheEnvVar,
                            EnvUtil.MsalFileCacheLocationEnvVar
                        ));
                    return 0;
                }

                // Plug-in mode
                if (parsedArgs.Plugin)
                {
                    try
                    {
                        using (IPlugin plugin = await PluginFactory.CreateFromCurrentProcessAsync(requestHandlers, ConnectionOptions.CreateDefault(), tokenSource.Token).ConfigureAwait(continueOnCapturedContext: false))
                        {
                            multiLogger.Add(new PluginConnectionLogger(plugin.Connection));
                            multiLogger.Verbose(Resources.RunningInPlugin);
                            multiLogger.Verbose(string.Format(Resources.CommandLineArgs, PlatformInformation.GetProgramVersion(), Environment.CommandLine));
                            EnvUtil.SetProgramContextInEnvironment(Context.NuGet);

                            await WaitForPluginExitAsync(plugin, multiLogger, TimeSpan.FromMinutes(2)).ConfigureAwait(continueOnCapturedContext: false);
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        // When restoring from multiple sources, one of the sources will throw an unhandled TaskCanceledException
                        // if it has been restored successfully from a different source.

                        // This is probably more confusing than interesting to users, but may be helpful in debugging,
                        // so log the exception but not to the console.
                        multiLogger.Log(LogLevel.Verbose, allowOnConsole:false, ex.ToString());
                    }

                    return 0;
                }

                // Stand-alone mode
                if (requestHandlers.TryGet(MessageMethod.GetAuthenticationCredentials, out IRequestHandler requestHandler) && requestHandler is GetAuthenticationCredentialsRequestHandler getAuthenticationCredentialsRequestHandler)
                {
                    // When emitting machine-readable output to standard out, logging (including Device Code prompts) must be emitted to standard error
                    if (parsedArgs.OutputFormat == OutputFormat.Json)
                    {
                        multiLogger.Add(new StandardErrorLogger());
                    }
                    else
                    {
                        multiLogger.Add(new StandardOutputLogger());
                    }

                    multiLogger.SetLogLevel(parsedArgs.Verbosity);
                    multiLogger.Verbose(Resources.RunningInStandAlone);
                    multiLogger.Verbose(string.Format(Resources.CommandLineArgs, PlatformInformation.GetProgramVersion(), Environment.CommandLine));

                    if (parsedArgs.Uri == null)
                    {
                        Console.WriteLine(ArgUsage.GenerateUsageFromTemplate<CredentialProviderArgs>());
                        return 1;
                    }

                    GetAuthenticationCredentialsRequest request = new GetAuthenticationCredentialsRequest(parsedArgs.Uri, isRetry: parsedArgs.IsRetry, isNonInteractive: parsedArgs.NonInteractive, parsedArgs.CanShowDialog);
                    GetAuthenticationCredentialsResponse response = await getAuthenticationCredentialsRequestHandler.HandleRequestAsync(request).ConfigureAwait(continueOnCapturedContext: false);

                    // Fail if credentials are not found
                    if (response?.ResponseCode != MessageResponseCode.Success)
                    {
                        return 2;
                    }

                    string resultUsername = response?.Username;
                    string resultPassword = parsedArgs.RedactPassword ? Resources.Redacted : response?.Password;
                    if (parsedArgs.OutputFormat == OutputFormat.Json)
                    {
                        // Manually write the JSON output, since we don't use ConsoleLogger in JSON mode (see above)
                        Console.WriteLine(JsonSerializer.Serialize(new CredentialResult(resultUsername, resultPassword)));
                    }
                    else
                    {
                        multiLogger.Info($"{Resources.Username}: {resultUsername}");
                        multiLogger.Info($"{Resources.Password}: {resultPassword}");
                    }
                    return 0;
                }

                return -1;
            }
            finally
            {
                foreach (ICredentialProvider credentialProvider in credentialProviders)
                {
                    credentialProvider.Dispose();
                }
            }
        }

        internal static async Task WaitForPluginExitAsync(IPlugin plugin, ILogger logger, TimeSpan shutdownTimeout)
        {
            var beginShutdownTaskSource = new TaskCompletionSource<object>();
            var endShutdownTaskSource = new TaskCompletionSource<object>();

            plugin.Connection.Faulted += (sender, a) =>
            {
                logger.Error(string.Format(Resources.FaultedOnMessage, $"{a.Message?.Type} {a.Message?.Method} {a.Message?.RequestId}"));
                logger.Error(a.Exception.ToString());
            };

            plugin.BeforeClose += (sender, args) =>
            {
                Volatile.Write(ref shuttingDown, true);
                beginShutdownTaskSource.TrySetResult(null);
            };

            plugin.Closed += (sender, a) =>
            {
                // beginShutdownTaskSource should already be set in BeforeClose, but just in case do it here too
                beginShutdownTaskSource.TrySetResult(null);

                endShutdownTaskSource.TrySetResult(null);
            };

            await beginShutdownTaskSource.Task;
            using (new Timer(_ => endShutdownTaskSource.TrySetCanceled(), null, shutdownTimeout, TimeSpan.FromMilliseconds(-1)))
            {
                await endShutdownTaskSource.Task;
            }

            if (endShutdownTaskSource.Task.IsCanceled)
            {
                logger.Error(Resources.PluginTimedOut);
            }
        }

        private static ILogger GetFileLogger()
        {
            var location = EnvUtil.FileLogLocation;
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(location));

            return new LogEveryMessageFileLogger(location);
        }
    }
}
