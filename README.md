# Azure Artifacts Credential Provider

The Azure Artifacts Credential Provider automates the acquisition of credentials needed to restore NuGet packages as part of your .NET development workflow. It integrates with MSBuild, dotnet, and NuGet(.exe) and works on Windows, Mac, and Linux. Any time you want to use packages from an Azure Artifacts feed, the Credential Provider will automatically acquire and securely store a token on behalf of the NuGet client you're using.

[![Build Status](https://dev.azure.com/mseng/AzureDevOps/_apis/build/status/MSCredProvider.CI?branchName=master)](https://dev.azure.com/mseng/AzureDevOps/_build/latest?definitionId=7110&branchName=master)

-   [Prerequisites](#prerequisites)
-   [Setup](#setup)
-   [Use](#use)
-   [Session Token Cache Locations](#session-token-cache-locations)
-   [Environment Variables](#environment-variables)
-   [Help](#help)
-   [Contribute](#contribute)

## Prerequisites

### MSBuild on Windows

Install [Visual Studio version 15.9-preview1 or later](https://visualstudio.microsoft.com/vs/preview/) to get the required version of msbuild (`15.8.166.59604` or later).

### NuGet

[NuGet(.exe)](https://www.nuget.org/downloads) on the command line version `4.8.0.5385` or later is required.

### dotnet

[dotnet SDK](https://www.microsoft.com/net/download) version `2.1.400` or later is required on Windows.

## Setup

If you are using `dotnet` or `nuget`, you can use the Azure Artifact Credential Provider by adding it to [NuGet's plugin search path](https://github.com/NuGet/Home/wiki/NuGet-Cross-Plat-Credential-Plugin#plugin-installation-and-discovery). This section contains both manual and scripted instructions for doing so.

### Manual installation: Windows

1. Download the latest release of [Microsoft.NuGet.CredentialProvider.zip](https://github.com/Microsoft/artifacts-credprovider/releases)
2. Unzip the file
3. Copy the `netcore` directory from the extracted archive to `$env:UserProfile\.nuget\plugins`

Note: copying the `netfx` directory is not recommended unless you don't plan to install an edition of Visual Studio (including the Build Tools edition). All Visual Studio editions come with (and automatically update) the `netfx` version of the Credential Provider.

### Automatic PowerShell installation:

[PowerShell helper script](helpers/installcredprovider.ps1)
To install netcore, run `installcredprovider.ps1`
To install both netfx and netcore, run `installcredprovider.ps1 -AddNetfx`

### Manual installation: Linux and Mac

1. Download the latest release of [Microsoft.NuGet.CredentialProvider.tar.gz](https://github.com/Microsoft/artifacts-credprovider/releases)
2. Untar the file
3. Copy the `netcore` directory from the extracted archive to `$HOME\.nuget\plugins`

### Automatic installation: bash, zsh, etc.

[Linux or Mac helper script](helpers/installcredprovider.sh)

## Use

Because the Credential Provider is a NuGet plugin, it is most commonly used indirectly, by performing a NuGet operation that requires authentication using `dotnet`, `nuget`, or `msbuild`.

### dotnet

The first time you perform an operation that requires authentication using `dotnet`, you must either use the `--interactive` flag to allow `dotnet` to prompt you for credentials, or provide them via an environment variable. 

If you're running interactively navigate to your project directory and run:

```shell
dotnet restore --interactive
```

Once you've successfully acquired a token, you can run authenticated commands without the `--interactive` flag for the lifespan of the token which is saved in the [session token cache location](#session-token-cache-locations).

If you're running the command as part of an automated build on an unattended build agent, you can supply an access token directly using the `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS` [environment variable](#environment-variables).

### nuget

The nuget client will prompt for authentication when you run a `restore` and it does not find credential in the [session token cache location](#session-token-cache-locations). By default, it will attempt to open a dialog for authentication and will fall back to console input if that fails.

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

The Credential Provider will save session tokens in the following locations:

-   Windows: `$env:UserProfile\AppData\Local\MicrosoftCredentialProvider`
-   Linux/MAC: `$HOME/.local/share/MicrosoftCredentialProvider/`

## Environment Variables

The Credential Provider accepts a set of environment variables. These are the only two that we recommend setting in non-triage situations.

-   `NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED`: Controls whether or not the session token is saved to disk. If false, the Credential Provider will prompt for auth every time.
-   `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS`: Json that contains an array of service endpoints, usernames and access tokens to authenticate endpoints in nuget.config. Example:

```javascript
 {"endpointCredentials": [{"endpoint":"http://example.index.json", "username":"optional", "password":"accesstoken"}]}
```

You may need to restart the agent service or the computer before the environment variables are available to the agent.

## Help

The windows plugin, delivered in the `netfx` folder of `Microsoft.NuGet.CredentialProvider.zip`, ships a stand-alone executable that will acquire credentials. This program will place the credentials in the same location that the .dll would if it were called by nuget.exe, dotnet.exe, or msbuild.exe. The stand-alone executable will also print the available command options, environment variables, and credential storage locations. This information is reproduced here:

```
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
    behavior of the Credential Provider. They may be used for workarounds but their
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
        The default for Personal Access Tokens is 90 days.
        The default for JWT (self-describing) tokens is 4 hours.
        The maximum allowed validity period for JWT tokens is 24 hours.

Token Type
    NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE
        Specify 'Compact' to generate a Personal Access Token, which may
        have a long validity period as it can easily be revoked from the UI,
        and sends a notification mail on creation.
        Specify 'SelfDescribing' to generate a shorter-lived JWT token,
        which does not appear in any UI or notifications
        and is more difficult to revoke.
        By default PATs are generated rather than JWTs,
        unless authentication can be performed non-interactively.

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
        Example: {"endpointCredentials": [{"endpoint":"http://example.index.json",
        "username":"optional", "password":"accesstoken"}]}

Cache Location
    The Credential Provider uses the following paths to cache credentials. If
    deleted, the Credential Provider will re-create them but any credentials
    will need to be provided again.

    ADAL Token Cache
    C:\Users\someuser\AppData\Local\MicrosoftCredentialProvider\ADALTokenCache.dat

    Session Token Cache
    C:\Users\someuser\AppData\Local\MicrosoftCredentialProvider\SessionTokenCache.dat

Windows Integrated Authentication
    NUGET_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED
        Boolean to enable/disable using silent Windows Integrated Authentication
        to authenticate as the logged-in user. Enabled by default.

Device Flow Authentication Timeout
    NUGET_CREDENTIALPROVIDER_VSTS_DEVICEFLOWTIMEOUTSECONDS
        Device Flow authentication timeout in seconds. Default is 90 seconds.
```

## Contribute

This project welcomes contributions and suggestions; see [CONTRIBUTING](CONTRIBUTING.md) for more information.
Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
