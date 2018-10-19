// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        internal static string Name => name.Value;
        internal static string Version => version.Value;

        internal static IList<ProductInfoHeaderValue> UserAgent
        {
            get
            {
                return new List<ProductInfoHeaderValue>()
                {
                    new ProductInfoHeaderValue(Name, Version),
#if NETFRAMEWORK
                    new ProductInfoHeaderValue("(netfx)"),
#else
                    new ProductInfoHeaderValue("(netcore)"),
#endif
                };
            }
        }

        private static Lazy<string> name = new Lazy<string>(() =>
        {
            return typeof(Program).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "CredentialProvider.Microsoft";
        });

        private static Lazy<string> version = new Lazy<string>(() =>
        {
            return typeof(Program).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        });

        public static async Task<int> Main(string[] args)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            var parsedArgs = await Args.ParseAsync<CredentialProviderArgs>(args);
            var multiLogger = new MultiLogger();

            var fileLogger = GetFileLogger();
            if (fileLogger != null)
            {
                multiLogger.Add(fileLogger);
            }

            // Cancellation listener
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs eventArgs) =>
            {
                // ConsoleCancelEventArgs.Cancel defaults to false which terminates the current process.
                tokenSource.Cancel();
            };

            List<ICredentialProvider> credentialProviders = new List<ICredentialProvider>
            {
                new VstsBuildTaskServiceEndpointCredentialProvider(multiLogger),
                new VstsBuildTaskCredentialProvider(multiLogger),
                new VstsCredentialProvider(multiLogger),
            };

            try
            {
                IRequestHandlers requestHandlers = new RequestHandlerCollection
                {
                    { MessageMethod.GetAuthenticationCredentials, new GetAuthenticationCredentialsRequestHandler(multiLogger, credentialProviders) },
                    { MessageMethod.GetOperationClaims, new GetOperationClaimsRequestHandler(multiLogger, credentialProviders) },
                    { MessageMethod.Initialize, new InitializeRequestHandler(multiLogger) },
                    { MessageMethod.SetLogLevel, new SetLogLevelRequestHandler(multiLogger) },
                    { MessageMethod.SetCredentials, new SetCredentialsRequestHandler(multiLogger) },
                };

                multiLogger.Verbose(string.Format(Resources.CommandLineArgs, Program.Version, Environment.CommandLine));

                // Help
                if (parsedArgs.Help)
                {
                    Console.WriteLine(string.Format(Resources.CommandLineArgs, Program.Version, Environment.CommandLine));
                    Console.WriteLine(ArgUsage.GenerateUsageFromTemplate<CredentialProviderArgs>());
                    Console.WriteLine(
                        string.Format(
                            Resources.EnvironmentVariableHelp,
                            EnvUtil.LogPathEnvVar,
                            EnvUtil.SessionTokenCacheEnvVar,
                            EnvUtil.AuthorityEnvVar,
                            EnvUtil.AdalFileCacheEnvVar,
                            EnvUtil.PpeHostsEnvVar,
                            EnvUtil.SupportedHostsEnvVar,
                            EnvUtil.SessionTimeEnvVar,
                            EnvUtil.TokenTypeEnvVar,
                            EnvUtil.BuildTaskUriPrefixes,
                            EnvUtil.BuildTaskAccessToken,
                            EnvUtil.BuildTaskExternalEndpoints,
                            EnvUtil.AdalTokenCacheLocation,
                            EnvUtil.SessionTokenCacheLocation
                        ));
                    return 0;
                }

                // Plug-in mode
                if (parsedArgs.Plugin)
                {
                    multiLogger.Verbose(Resources.RunningInPlugin);

                    using (IPlugin plugin = await PluginFactory.CreateFromCurrentProcessAsync(requestHandlers, ConnectionOptions.CreateDefault(), tokenSource.Token).ConfigureAwait(continueOnCapturedContext: false))
                    {
                        multiLogger.Add(new PluginConnectionLogger(plugin.Connection));
                        await RunNuGetPluginsAsync(plugin, multiLogger, TimeSpan.FromMinutes(2), tokenSource.Token).ConfigureAwait(continueOnCapturedContext: false);
                    }

                    return 0;
                }

                // Stand-alone mode
                if (requestHandlers.TryGet(MessageMethod.GetAuthenticationCredentials, out IRequestHandler requestHandler) && requestHandler is GetAuthenticationCredentialsRequestHandler getAuthenticationCredentialsRequestHandler)
                {
                    multiLogger.Add(new ConsoleLogger());
                    multiLogger.SetLogLevel(parsedArgs.Verbosity);
                    multiLogger.Verbose(Resources.RunningInStandAlone);

                    if (parsedArgs.Uri == null)
                    {
                        Console.WriteLine(ArgUsage.GenerateUsageFromTemplate<CredentialProviderArgs>());
                        return 1;
                    }

                    GetAuthenticationCredentialsRequest request = new GetAuthenticationCredentialsRequest(parsedArgs.Uri, isRetry: parsedArgs.IsRetry, isNonInteractive: parsedArgs.NonInteractive, parsedArgs.CanShowDialog);
                    GetAuthenticationCredentialsResponse response = await getAuthenticationCredentialsRequestHandler.HandleRequestAsync(request).ConfigureAwait(continueOnCapturedContext: false);

                    multiLogger.Info($"{Resources.Username}: {response?.Username}");
                    multiLogger.Info($"{Resources.Password}: {(parsedArgs.RedactPassword ? Resources.Redacted : response?.Password)}");
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

        internal static async Task RunNuGetPluginsAsync(IPlugin plugin, ILogger logger, TimeSpan timeout, CancellationToken cancellationToken)
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(0);

            plugin.Connection.Faulted += (sender, a) =>
            {
                logger.Error(string.Format(Resources.FaultedOnMessage, $"{a.Message?.Type} {a.Message?.Method} {a.Message?.RequestId}"));
                logger.Error(a.Exception.ToString());
            };

            plugin.Closed += (sender, a) => semaphore.Release();

            bool complete = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            if (!complete)
            {
                logger.Error(Resources.PluginTimedOut);
            }
        }

        private static FileLogger GetFileLogger()
        {
            var location = EnvUtil.FileLogLocation;
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(location));
            var fileLogger = new FileLogger(location);
            fileLogger.SetLogLevel(NuGet.Common.LogLevel.Verbose);

            return fileLogger;
        }
    }
}