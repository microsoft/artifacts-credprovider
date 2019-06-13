// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using NuGet.Common;
using PowerArgs;

namespace NuGetCredentialProvider
{
    internal class CredentialProviderArgs
    {
        [ArgDescription("Used by nuget to run the credential helper in plugin mode")]
        public bool Plugin { get; set; }

        [ArgDescription("The package source URI for which credentials will be filled")]
        public Uri Uri { get; set; }

        [ArgDescription("If present and true, providers will not issue interactive prompts")]
        public bool NonInteractive { get; set; }

        [ArgDescription("Always generate fresh credentials, ignoring the cache")]
        [OmitFromUsageDocs] // IsRetry support is really for the NuGet flow and is confusing for standalone. Keep for backcompat, but don't advertise.
        public bool IsRetry { get; set; }

        [ArgDescription("Skip validating cached credentials. Expired or revoked credentials may be returned.")]
        [ArgShortcut("S")]
        public bool SkipValidatingCachedCreds { get; set; }

        [ArgDefaultValue(LogLevel.Information)]
        [ArgDescription("Display this amount of detail in the output")]
        public LogLevel Verbosity { get; set; }

        [ArgDescription("Prevents writing the password to standard output (for troubleshooting purposes)")]
        public bool RedactPassword { get; set; }

        [ArgShortcut("?")]
        [ArgShortcut("h")]
        [ArgDescription("Prints this help message")]
        public bool Help { get; set; }

        [ArgDescription("If true, user can be prompted with credentials through UI, if false, device flow must be used")]
        public bool CanShowDialog { get; set; }

        [ArgDescription("In standalone mode, format the results for human readability or as JSON. If JSON is selected, then logging (which may include Device Code instructions) will be logged to standard error instead of standard output.")]
        [ArgShortcut("F")]
        public OutputFormat OutputFormat { get; set; }
    }

    public enum OutputFormat
    {
        HumanReadable = 0,
        Json = 1
    }
}
