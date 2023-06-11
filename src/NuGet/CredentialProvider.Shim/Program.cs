// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Diagnostics;
using PowerArgs;

namespace NuGetCredentialProvider;

internal enum Verbosity
{
    Normal,
    Quiet,
    Detailed
}

internal class CredentialProviderArgs
{
    public Uri Uri { get; set; }

    public bool NonInteractive { get; set; }

    public bool IsRetry { get; set; }

    [ArgDefaultValue(Verbosity.Normal)]
    public Verbosity Verbosity { get; set; }
}

internal class Program
{
    private static int Main(string[] args)
    {
        var parsedArgs = Args.Parse<CredentialProviderArgs>(args);

        return RunCredentialProvider(parsedArgs);
    }

    private static int RunCredentialProvider(CredentialProviderArgs args)
    {
        string verbosity = args.Verbosity switch
        {
            Verbosity.Normal => "Information",
            Verbosity.Quiet => "Minimal",
            Verbosity.Detailed => "Debug",
            _ => "Information"
        };

        var startInfo = new ProcessStartInfo()
        {
            FileName = @"CredentialProvider\CredentialProvider.Microsoft.exe",
            Arguments = $"-Uri {args.Uri}{(args.NonInteractive ? " -NonInteractive" : "")}{(args.IsRetry ? " -IsRetry" : "")} -Verbosity {verbosity} -OutputFormat Json",
            WorkingDirectory = "CredentialProvider",
            CreateNoWindow = false, // Need console window to be created
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = new Process();

        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Out.WriteLine(e.Data);
            }
        };
        process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        return process.ExitCode;
    }
}