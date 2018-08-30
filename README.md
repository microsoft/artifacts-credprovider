# TOC
- [Azure Artifacts Credential Provider](#azure-artifacts-credential-provider)
  * [Prerequisites](#prerequisites)
    + [MSBuild on Windows](#msbuild-on-windows)
    + [Nuget](#nuget)
    + [Dotnet](#dotnet)
  * [Setup](#setup)
    + [Manual Instructions for Windows](#manual-instructions-for-windows)
    + [Manual Instructions for Linux and Mac](#manual-instructions-for-linux-and-mac)
    + [Shell (bash, zsh, etc.)](#shell-bash-zsh-etc)
    + [PowerShell](#powershell)
  * [Use](#use)
    + [dotnet](#dotnet)
    + [nuget](#nuget)
    + [msbuild](#msbuild)
  * [Session Token Cache Locations](#session-token-cache-locations)
  * [Environment Variables](#environment-variables)
  * [Help](#help)
  * [Develop](#develop)
    + [Building](#building)
    + [Publishing](#publishing)
    + [Packing](#packing)
    + [Versioning](#versioning)
  * [Contribute](#contribute)

# Azure Artifacts Credential Provider
The Azure Artifacts Credential Provider automates the acquisition of credentials needed to restore NuGet packages as part of your .NET development workflow. It integrates with MSBuild, dotnet, and NuGet(.exe) and works on Windows, Mac, and Linux. Any time you want to use packages from an Azure Artifacts feed, the Credential Provider will automatically acquire and securely store a token on behalf of the NuGet client you're using.

[![Build status](https://mseng.visualstudio.com/_apis/public/build/definitions/b924d696-3eae-4116-8443-9a18392d8544/7110/badge?branchName=master)](https://mseng.visualstudio.com/VSOnline/_build/latest?definitionId=7110&branch=master)

## Prerequisites

### MSBuild on Windows
Install any version of [Visual Studio version 15.9-preview1 or later](https://visualstudio.microsoft.com/vs/preview/) to get the required version of msbuild (i.e. `15.8.166.59604` or later).

### Nuget
If you are using nuget without the Visual Studio IDE to restore packages then you must install version `4.8.0.5385` or later.

### Dotnet
Dotnet SDK `2.1.400` or higher is required on Windows, Linux and Mac.

## Setup
If you are using the either the [dotnet SDK](https://www.microsoft.com/net/download) or [nuget](https://www.nuget.org/downloads) directly then you need to add the credential provider's plugin implementation to nuget's plugin search path. Below we have provided instructions for doing this manually or with platform specific scripts.

### Manual Instructions for Windows
1) Download the latest release of [`Microsoft.NuGet.CredentialProvider.zip`](https://github.com/Microsoft/mscredprovider/releases).
2) Unzip the file.
3) Copy both the `netcore` and `netfx` directories from the extracted archive to `$env:UserProfile\.nuget\plugins`

### Manual Instructions for Linux and Mac
1) Download the latest release of [`Microsoft.NuGet.CredentialProvider.tar.gz`](https://github.com/Microsoft/mscredprovider/releases).
2) Unzip the file.
3) Copy both the `netcore` directory from the extracted archive to `$HOME\.nuget\plugins`


### Shell (bash, zsh, etc.)

```shell
[command]
```

### PowerShell

```powershell
[command]
```

## Use

Because the Credential Provider is a NuGet plugin, it is most commonly used indirectly, by performing a NuGet operation that requires authentication using `dotnet`, `nuget`, or `msbuild`.

### dotnet

The first time you perform an operation that requires authentication using `dotnet`, you must use the `--interactive` flag to allow `dotnet` to prompt you for credentials. For example, to restore packages, navigate to your project directory and run:

```shell
dotnet restore --interactive
```

Once you've successfully acquired a token, you can run authenticated commands without the `--interactive` flag for the lifespan of the token which is saved in the [session token cache location](#session-token-cache-locations).

### nuget

The nuget client will prompt for authentication when you run a `restore` and it does not find credential in the [session token cache location](#session-token-cache-locations).  By default, it will attempt to open a modal dialog for authentication and will fall back to console input if that fails.

```shell
nuget restore
```

### msbuild

The first time you perform an operation that requires authentication using `msbuild`, you must use the `/p:nugetInteractive=true` switch to allow `msbuild` to prompt you for credentials. For example, to restore packages, navigate to your project or solution directory and run:

```shell
msbuild /t:restore /p:nugetInteractive=true
```

Once you've successfully acquired a token, you can run authenticated commands without the `/p:nugetInteractive=true` switch.

## Session Token Cache Locations
The credential provider will save session tokens in the following locations:
- Windows: `$env:UserProfile\AppData\Local\MicrosoftCredentialProvider`
- Linux/MAC: `$HOME/.local/share/MicrosoftCredentialProvider/`

## Environment Variables
This is not an exhaustive list.  These are the environment variables that might make sense for users to set.

- `NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED`: Controls whether or not the session token is saved to disk.  If false, the credential provider will prompt for auth every time.
- `VSS_NUGET_ACCESSTOKEN`: This variable is useful for headless/unattended scenarios where you already have an auth token.  If you set this variable the credential provider will skip any attempt at authentication with AAD and simply return this value to nuget, dotnet, or msbuild.  This is useful for build scenarios and docker where you must have a precalculated Personal Access Token.
- `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS`: Json that contains an array of service endpoints, usernames and access tokens to authenticate endpoints in nuget.config. Example:
```
 {"endpointCredentials": ["endpoint":"http://example.index.json", "username":"optional", "password":"accesstoken"]}"
```

## Help

The windows plugin, delivered in the `netfx` folder of `Microsoft.NuGet.CredentialProvider.zip`, ships a stand-alone executable that will acquire credentials. This program, , will place the credentials in the same location (i.e. ) that the .dll would if it were called by nuget.exe, dotnet.exe, or msbuild.exe. The stand-alone executable will also print the available command options, environment variables, and credential storage locations. This information is reproduced here:

```shell
C:\> .\CredentialProvider.Microsoft.exe -h
Command-line v0.1.4: "CredentialProvider.Microsoft.exe" -h
Usage - CredentialProvider.Microsoft -options

GlobalOption          Description
Plugin (-P)           Used by nuget to run the credential helper in plugin mode
Uri (-U)              The package source URI for which credentials will be filled
NonInteractive (-N)   If present and true, providers will not issue interactive prompts
IsRetry (-I)          Notifies the provider that this is a retry and the credentials were rejected on a previous attempt
Verbosity (-V)        Display this amount of detail in the output [Default='Information']
                      Debug
                      Verbose
                      Information
                      Minimal
                      Warning
                      Error
RedactPassword (-R)   Prevents writing the password to standard output (for troubleshooting purposes)
Help (-?, -h)         Prints this help message
CanShowDialog (-C)    If true, user can be prompted with credentials through UI, if false, device flow must be used

List of Environment Variables
    The following is a list of environment variables that can be used to modify the
    behavior of the credential provider. They may be used for workarounds but their
    use is not supported. Use at your own risk.

Log Path
    NUGET_CREDENTIALPROVIDER_LOG_PATH
        Absolute path to a log file where the provider will write log messages.

Session Token Cache Enabled
    NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED
        Boolean to enable/disable the Session Token cache.

ADAL Authority
    NUGET_CREDENTIALPROVIDER_ADAL_AUTHORITY
        Set to override the authority used when fetching an ADAL token.
        e.g. https://login.microsoftonline.com

ADAL Token File Cache Enabled
    NUGET_CREDENTIALPROVIDER_ADAL_FILECACHE_ENABLED
        Boolean to enable/disable the ADAL token cache.

PPE ADAL Hosts
    NUGET_CREDENTIALPROVIDER_ADAL_PPEHOSTS
        Semi-colon separated list of hosts that should use the PPE environment
        when fetching ADAL tokens. Should only be used for testing/development
        environments such as DevFabric.

Supported Hosts
    NUGET_CREDENTIALPROVIDER_VSTS_HOSTS
        Semi-colon separated list of hosts that the ADAL provider supports.

Session Token Time Validity
    NUGET_CREDENTIALPROVIDER_VSTS_SESSIONTIMEMINUTES
        Time in minutes the generated Session Tokens will be valid for.

Build Provider URI Prefixes
    VSS_NUGET_URI_PREFIXES
        Semi-colon separated list of hosts the build provider supports.

Build Provider Access Token
    VSS_NUGET_ACCESSTOKEN
        The Personal Access Token that will be returned as credentials by
        the build provider.

Build Provider Service Endpoint Json
    VSS_NUGET_EXTERNAL_FEED_ENDPOINTS
        Json that contains an array of service endpoints, usernames and
        access tokens to authenticate endpoints in nuget.config.
        Example: "{"endpointCredentials": ["endpoint":"http://example.index.json",
        "username":"optional", "password":"accesstoken"]}"

Cache Location
    The Credential Provider uses the following paths to cache credentials. If
    deleted, the credential provider will re-create them but any credentials
    will need to be provided again.

    ADAL Token Cache
    C:\Users\kerobert\AppData\Local\MicrosoftCredentialProvider\ADALTokenCache.dat

    Session Token Cache
    C:\Users\kerobert\AppData\Local\MicrosoftCredentialProvider\SessionTokenCache.dat
```

## Develop

### Building

```shell
dotnet build CredentialProvider.Microsoft --configuration Release
```

In this and subsequent examples, configuration can be either `debug` or `release`.

### Publishing

```shell
dotnet publish CredentialProvider.Microsoft --configuration Release --framework netcoreapp2.1
```

### Packing

```shell
dotnet pack CredentialProvider.Microsoft --configuration Release
```

For CI builds, you can append a pre-release version:

```shell
dotnet pack CredentialProvider.Microsoft --configuration Release /p:NuspecProperties=VersionSuffix=-MyCustomVersion-2
```

### Versioning

When releasing a new version, update the CredentialProviderVersion property in Build.props

## Contribute

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
