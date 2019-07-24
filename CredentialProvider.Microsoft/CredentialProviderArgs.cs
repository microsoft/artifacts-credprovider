// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using NuGet.Common;
using PowerArgs;

namespace NuGetCredentialProvider
{
    [ArgDescription("The Azure Artifacts credential provider can be used to aquire credentials for Azure Artifacts.\n" +
                    "\n" +
                    "Note: The Azure Artifacts Credential Provider is mainly intended for use via integrations with development tools such as .NET Core and nuget.exe.\n" + 
                    "While it can be used via this CLI in \"stand-alone mode\", please pay special attention to certain options such as -IsRetry below.\n" +
                    "Failing to do so may result in obtaining invalid credentials.")]
    internal class CredentialProviderArgs
    {
        [ArgDescription("Used by nuget to run the credential helper in plugin mode")]
        public bool Plugin { get; set; }

        [ArgDescription("The package source URI for which credentials will be filled")]
        public Uri Uri { get; set; }

        [ArgDescription("If present and true, providers will not issue interactive prompts")]
        public bool NonInteractive { get; set; }

        [ArgDescription("If false / unset, INVALID CREDENTIALS MAY BE RETURNED. The caller is required to validate returned credentials themselves, and if invalid, should call the credential provider again with -IsRetry set. If true, the credential provider will obtain new credentials instead of returning potentially invalid credentials from the cache.")]
        public bool IsRetry { get; set; }

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
