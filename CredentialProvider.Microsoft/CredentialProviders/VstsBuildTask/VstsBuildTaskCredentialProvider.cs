// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGetCredentialProvider.Logging;
using NuGetCredentialProvider.Util;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.CredentialProviders.VstsBuildTask
{
    public sealed class VstsBuildTaskCredentialProvider : CredentialProviderBase
    {
        private const string Username = "VssSessionToken";

        public VstsBuildTaskCredentialProvider(ILogger logger)
            : base(logger)
        {
        }

        protected override string LoggingName => nameof(VstsBuildTaskCredentialProvider);

        public override bool IsCachable { get { return false; } }

        public override Task<bool> CanProvideCredentialsAsync(Uri uri)
        {
            string uriPrefixesString = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskUriPrefixes);
            string accessToken = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskAccessToken);

            bool useBuildTaskCredProvider = string.IsNullOrWhiteSpace(uriPrefixesString) == false && string.IsNullOrWhiteSpace(accessToken) == false;
            if (useBuildTaskCredProvider == true)
            {
                return Task.FromResult(true);
            }

            Verbose(Resources.BuildTaskEnvVarError);
            return Task.FromResult(false);
        }

        public override Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string uriPrefixesString = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskUriPrefixes);
            string accessToken = Environment.GetEnvironmentVariable(EnvUtil.BuildTaskAccessToken);

            string[] uriPrefixes = uriPrefixesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Verbose(Resources.BuildTaskUriPrefixes);
            foreach (var uriPrefix in uriPrefixes)
            {
                Verbose($"{uriPrefix}");
            }

            string uriString = request.Uri.ToString();
            string matchedPrefix = uriPrefixes.FirstOrDefault(prefix => uriString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            Verbose(string.Format(Resources.BuildTaskMatchedPrefix, matchedPrefix));

            if (matchedPrefix == null)
            {
                Verbose(Resources.BuildTaskNoPrefixMatch);
                return this.GetResponse(
                    null,
                    null,
                    Resources.BuildTaskNoPrefixMatch,
                    MessageResponseCode.Error);
            }

            return this.GetResponse(
                    Username,
                    accessToken,
                    null,
                    MessageResponseCode.Success);
        }

        private Task<GetAuthenticationCredentialsResponse> GetResponse(string username, string password, string message, MessageResponseCode responseCode)
        {
            return Task.FromResult(new GetAuthenticationCredentialsResponse(
                    username: username,
                    password: password,
                    message: message,
                    authenticationTypes: new List<string>
                    {
                        "Basic"
                    },
                    responseCode: responseCode));
        }
    }
}
