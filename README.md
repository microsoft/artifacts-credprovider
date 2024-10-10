﻿# Azure Artifacts Credential Provider

The Azure Artifacts Credential Provider automates the acquisition of credentials needed to restore NuGet packages as part of your .NET development workflow. It integrates with MSBuild, dotnet, and NuGet(.exe) and works on Windows, Mac, and Linux. Any time you want to use packages from an Azure Artifacts feed, the Credential Provider will automatically acquire and securely store a token on behalf of the NuGet client you're using.

[![Build Status](https://dev.azure.com/mseng/PipelineTools/_apis/build/status/artifacts-credprovider/microsoft.artifacts-credprovider.CI?branchName=master)](https://dev.azure.com/mseng/PipelineTools/_build/latest?definitionId=13881&branchName=master)

-   [Prerequisites](#prerequisites)
-   [Setup](#setup)
-   [Use](#use)
-   [Session Token Cache Locations](#session-token-cache-locations)
-   [Environment Variables](#environment-variables)
-   [Release version 1.0.0](#release-version-1.0.0)
-   [Upcoming version 2.0.0](#release-version-2.0.0)
-   [Help](#help)
-   [Contribute](#contribute)

## Prerequisites

### MSBuild on Windows

Install [Visual Studio version 15.9-preview1 or later](https://visualstudio.microsoft.com/vs/preview/) to get the required version of MSBuild (`15.8.166.59604` or later). Alternatively, you can download MSBuild directly by downloading the [Build Tools for Visual Studio](https://visualstudio.microsoft.com/downloads/?q=build+tools). MSBuild is also installed as a part of the [.NET Core SDK](https://dotnet.microsoft.com/download).

### NuGet

[NuGet(.exe)](https://www.nuget.org/downloads) on the command line version `4.8.0.5385` or later is required. The recommended NuGet version is `5.5.x` or later as it has some important bug fixes related to cancellations and timeouts.

### dotnet

The default installation requires the [dotnet runtime](https://www.microsoft.com/net/download) version `8.0.x` or later.

## Setup

If you are using `dotnet` or `nuget`, you can use the Azure Artifact Credential Provider by adding it to [NuGet's plugin search path](https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery). This section contains both manual and scripted instructions for doing so.

Dotnet needs the `netcore` version to be installed. NuGet and MSBuild need the `netfx` version to be installed.

### Installation on Windows

#### Automatic PowerShell script

[PowerShell helper script](helpers/installcredprovider.ps1)
- To install netcore, run `installcredprovider.ps1`
  - e.g. `iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"`
  - .NET 6 bits can be installed using `iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) } -InstallNet6"`
  - .NET 8 bits can be installed using `iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) } -InstallNet8"`
- To install both netfx and netcore, run `installcredprovider.ps1 -AddNetfx`. The netfx version is needed for nuget.exe.
  - e.g. `iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) } -AddNetfx"`
  - .NET Framework 4.8.1 support is available using the `-AddNetFx48` flag

#### Manual installation on Windows

1. Download the latest release of [Microsoft.NuGet.CredentialProvider.zip](https://github.com/Microsoft/artifacts-credprovider/releases)
2. Unzip the file
3. Copy the `netcore` (and `netfx` for nuget.exe) directory from the extracted archive to `$env:UserProfile\.nuget\plugins` (%UserProfile%/.nuget/plugins/)

Using the above is recommended, but as per [NuGet's plugin discovery rules](https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery), alternatively you can install the credential provider to a location you prefer, and then set the environment variable NUGET_PLUGIN_PATHS to the .exe of the credential provider found in plugins\netfx\CredentialProvider.Microsoft\CredentialProvider.Microsoft.exe. For example, $env:NUGET_PLUGIN_PATHS="my-alternative-location\CredentialProvider.Microsoft.exe". Note that if you are using both nuget and dotnet, this environment variable is not recommended due to this issue: https://github.com/NuGet/Home/issues/8151

### Installation on Linux and Mac

#### Automatic bash script

[Linux or Mac helper script](helpers/installcredprovider.sh)

Examples:
- `wget -qO- https://aka.ms/install-artifacts-credprovider.sh | bash`
- `sh -c "$(curl -fsSL https://aka.ms/install-artifacts-credprovider.sh)"`

> Note: this script only installs the netcore version of the plugin. If you need to have it working with mono msbuild, you will need to download the version with both netcore and netfx binaries following the steps in [Manual installation on Linux and Mac](#installation-on-linux-and-mac)

#### Manual installation on Linux and Mac

1. Download the latest release of [Microsoft.NuGet.CredentialProvider.tar.gz](https://github.com/Microsoft/artifacts-credprovider/releases)
2. Untar the file
3. Copy the `netcore` (and 'netfx' for msbuild /t:restore command) directory from the extracted archive to `$HOME/.nuget/plugins`

Using the above is recommended, but as per [NuGet's plugin discovery rules](https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery), alternatively you can install the credential provider to a location you prefer, and then set the environment variable NUGET_PLUGIN_PATHS to the .dll of the credential provider found in plugins\netcore\CredentialProvider.Microsoft\CredentialProvider.Microsoft.dll. For example, $env:NUGET_PLUGIN_PATHS="my-alternative-location\CredentialProvider.Microsoft.dll".

Users requiring .NET 6, such as ARM64 users, can manually download the .NET 6 version `Microsoft.Net6.NuGet.CredentialProvider` of the [1.0.0 release](https://github.com/microsoft/artifacts-credprovider/releases/tag/v1.0.0). Support for .NET 8 was added in [release 1.3.0](https://github.com/microsoft/artifacts-credprovider/releases/tag/v1.3.0) and can be downloaded with the `Microsoft.Net8.NuGet.CredentialProvider` archive.

### Automatic usage
- MSBuild in Visual Studio Developer Command Prompt with Visual Studio 15.9+
- Azure DevOps Pipelines NuGetAuthenticate task
- Azure DevOps Pipelines NuGet task, NuGetCommandV2 version 2.145.3+ (Azure DevOps Server 2019 Update 1+)

## Use

Because the Credential Provider is a NuGet plugin, it is most commonly used indirectly, by performing a NuGet operation that requires authentication using `dotnet`, `nuget`, or `msbuild`.

### dotnet

The first time you perform an operation that requires authentication using `dotnet`, you must either use the `--interactive` flag to allow `dotnet` to prompt you for credentials, or provide them via an environment variable.

If you're running interactively navigate to your project directory and run:

```shell
dotnet restore --interactive
```

Once you've successfully acquired a token, you can run authenticated commands without the `--interactive` flag for the lifespan of the token which is saved in the [session token cache location](#session-token-cache-locations).

### nuget

The nuget client will prompt for authentication when you run a `restore` and it does not find credential in the [session token cache location](#session-token-cache-locations). By default, it will attempt to open a dialog for authentication and will fall back to console input if that fails.

```shell
nuget restore
```

When using Windows and you are already signed in to Azure DevOps, Windows Integrated Authentication may be used to get automatically authenticated as the signed in user.

### msbuild

The first time you perform an operation that requires authentication using `msbuild`, you must use the `/p:nugetInteractive=true` switch to allow `msbuild` to prompt you for credentials. For example, to restore packages, navigate to your project or solution directory and run:

```shell
msbuild /t:restore /p:nugetInteractive=true
```

Once you've successfully acquired a token, you can run authenticated commands without the `/p:nugetInteractive=true` switch.

### Unattended build agents

#### Azure DevOps Pipelines
Use the [NuGet Authenticate](https://docs.microsoft.com/en-us/azure/devops/pipelines/tasks/package/nuget-authenticate?view=azure-devops) task before running NuGet, dotnet or MSBuild commands that need authentication.

#### Other automated build scenarios
If you're running the command as part of an automated build on an unattended build agent outside of Azure DevOps Pipelines, you can supply an access token directly using the `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS` [environment variable](#environment-variables). The use of [Personal Access Tokens](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops) is recommended. You may need to restart the agent service or the computer before the environment variables are available to the agent.

### Docker containers
[Managing NuGet credentials in Docker scenarios](https://github.com/dotnet/dotnet-docker/blob/master/documentation/scenarios/nuget-credentials.md#using-the-azure-artifact-credential-provider)

### Azure DevOps Server
The Azure Artifacts Credential Provider may not be necessary for an on-premises Azure DevOps Server on Windows. If the credential provider is needed, it cannot acquire credentials interactively, therefore, the VSS_NUGET_EXTERNAL_FEED_ENDPOINTS environment variable must be used as an alternative. Supply a [Personal Access Token](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops) directly using the `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS` [environment variable](#environment-variables).

From Azure DevOps Server 2020 RC1 forward, the NuGet Authenticate task can be used in Pipelines.

## Session Token Cache Locations

The Credential Provider will save session tokens in the following locations:

-   Windows: `$env:UserProfile\AppData\Local\MicrosoftCredentialProvider`
-   Linux/MAC: `$HOME/.local/share/MicrosoftCredentialProvider/`

## Environment Variables

The Credential Provider accepts a set of environment variables. Not all of them we recommend using in production, but these two are considered safe.

-   `NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED`: Controls whether or not the session token is saved to disk. If false, the Credential Provider will prompt for auth every time.
-   `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS`: Json that contains an array of service endpoints, usernames and access tokens to authenticate endpoints in nuget.config. Example:

    ```javascript
    {"endpointCredentials": [{"endpoint":"http://example.index.json", "username":"optional", "password":"accesstoken"}]}
    ```

-   `ARTIFACTS_CREDENTIALPROVIDER_FEED_ENDPOINTS`: Json that contains an array of endpoints, usernames and azure service principal information needed to authenticate to Azure Artifacts feed endponts. Example:
    ```javascript
    {"endpointCredentials": [{"endpoint":"http://example.index.json", "clientId":"required", "clientCertificateSubjectName":"optional", "clientCertificateFilePath":"optional"}]}
    ```

    - `endpoint`: Required. Feed url to authenticate.
    - `clientId`: Required for both Azure Managed Identites and Service Principals. For user assigned managed identities enter the Entra client id. For system assigned managed identities set the value to `system`.
    - `clientCertificateSubjectName`: Subject Name of the certificate located in the CurrentUser or LocalMachine certificate store. Optional field. Only used for service principal authentication.
    - `clientCertificateFilePath`: File path location of the certificate on the machine. Optional field. Only used by service principal authentication. 

## Release version 1.0.0

Release version [1.0.0](https://github.com/microsoft/artifacts-credprovider/releases/tag/v1.0.0) was released in March 2022. Netcore version 1.0.0 of the Artifacts Credential Provider requires .NET Core 3.1. Older versions than 1.0.0 required .NET Core 2.1. `Microsoft.NetCore2.NuGet.CredentialProvider` asset is no longer available. Use  `Microsoft.NetCore3.NuGet.CredentialProvider.zip` instead.

[1.0.0 release](https://github.com/microsoft/artifacts-credprovider/releases/tag/v1.0.0) also publishes the credential provider for .NET 6 users as `Microsoft.Net6.NuGet.CredentialProvider`.

## Release version 2.0.0

Release version 2.0.0 will be the next major version of artifacts-credprovider and will contain changes which end support for various .NET versions which have reached their end of support. It is planned for release in Q1 2025 to allow users to migrate their usage of the tool to the new .NET versions.

- .NET Framework 4.6.1 (End of Support April 26, 2022) - Replaced with .NET Framework 4.8.1
- .NET Core 3.1 (End of Support December 13, 2022) - Replaced with .NET 6/8

.NET 6 will reach its end of support on November 12, 2024. After v2.0.0 is released, a minor version of artifacts-credprovider will be published to deprecate .NET 6 compatible binaries.

- .NET 6 (End of Support November 12, 2024) - Replaced with .NET 8

## Help

The windows plugin, delivered in the `netfx` folder of `Microsoft.NuGet.CredentialProvider.zip`, ships a stand-alone executable that will acquire credentials. This program will place the credentials in the same location that the .dll would if it were called by nuget.exe, dotnet.exe, or msbuild.exe. The stand-alone executable will also print the available command options, environment variables, and credential storage locations. This information is reproduced here:

```
C:\> .\CredentialProvider.Microsoft.exe -h
Command-line v1.0.6: .\CredentialProvider.Microsoft.exe -h

The Azure Artifacts credential provider can be used to acquire credentials for Azure Artifacts.

Note: The Azure Artifacts Credential Provider is mainly intended for use via integrations with development tools such as .NET Core and nuget.exe.
While it can be used via this CLI in "stand-alone mode", please pay special attention to certain options such as -IsRetry below.
Failing to do so may result in obtaining invalid credentials.

Usage - CredentialProvider.Microsoft -options

GlobalOption          Description
Plugin (-P)           Used by nuget to run the credential helper in plugin mode
Uri (-U)              The package source URI for which credentials will be filled
NonInteractive (-N)   If present and true, providers will not issue interactive prompts
IsRetry (-I)          If false / unset, INVALID CREDENTIALS MAY BE RETURNED. The caller is required to validate returned credentials themselves, and if
                      invalid, should call the credential provider again with -IsRetry set. If true, the credential provider will obtain new credentials
                      instead of returning potentially invalid credentials from the cache.
Verbosity (-V)        Display this amount of detail in the output [Default='Information']
                      Debug
                      Verbose
                      Information
                      Minimal
                      Warning
                      Error
RedactPassword (-R)   Prevents writing the password to standard output (for troubleshooting purposes)
Help (-?, -h)         Prints this help message
CanShowDialog (-C)    If true, user can be prompted with credentials through UI, if false, device flow must be used [Default='True']
OutputFormat (-F)     In standalone mode, format the results for human readability or as JSON. If JSON is selected, then logging (which may include Device
                      Code instructions) will be logged to standard error instead of standard output.
                      HumanReadable
                      Json

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

Supported Hosts
    NUGET_CREDENTIALPROVIDER_VSTS_HOSTS
        Semi-colon separated list of hosts that the credential provider supports.

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
    deleted, the credential provider will re-create them but any credentials
    will need to be provided again.

    MSAL Token Cache
    C:\Users\someuser\AppData\Local\.IdentityService

    Session Token Cache
    C:\Users\someuser\AppData\Local\MicrosoftCredentialProvider\SessionTokenCache.dat

Windows Integrated Authentication
    NUGET_CREDENTIALPROVIDER_WINDOWSINTEGRATEDAUTHENTICATION_ENABLED
        Boolean to enable/disable using silent Windows Integrated Authentication
        to authenticate as the logged-in user. Enabled by default.

Device Flow Authentication Timeout
    NUGET_CREDENTIALPROVIDER_VSTS_DEVICEFLOWTIMEOUTSECONDS
        Device Flow authentication timeout in seconds. Default is 90 seconds.

NuGet workarounds
    NUGET_CREDENTIALPROVIDER_FORCE_CANSHOWDIALOG_TO
        Set to "true" or "false" to override any other sources of the
        CanShowDialog parameter.

MSAL Authority
    NUGET_CREDENTIALPROVIDER_MSAL_AUTHORITY
        Set to override the authority used when fetching an MSAL token.
        e.g. https://login.microsoftonline.com/organizations

MSAL Token File Cache Enabled
    NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED
        Boolean to enable/disable the MSAL token cache. Enabled by default.

Provide MSAL Cache Location
    NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION
    Provide the location where the MSAL cache should be read and written to.

```

### Troubleshooting
#### How do I know the cred provider is installed correctly? / I'm still getting username/password prompt after installing
This means that either nuget/dotnet was unable to find the cred provider from [NuGet's plugin search path](https://github.com/microsoft/artifacts-credprovider#setup), or the cred provider failed to authenticate so the client defaulted to the username/password prompt. Verify the cred provider is correctly installed by checking it exists in the nuget/plugins folder in your user profile (Refer to the [setup docs](https://github.com/microsoft/artifacts-credprovider#setup)). If using nuget.exe and used the [install script](https://github.com/microsoft/artifacts-credprovider#automatic-powershell-script) to install the cred provider, please make sure you ran it with -AddNetfx.

#### How do I get better error logs from the cred provider?
Run the nuget.exe/dotnet command with detailed verbosity to see more cred provider logs that may help debugging (`nuget.exe -verbosity detailed`, `dotnet --verbosity detailed`).

#### How do I find out if my issue is a real 401?
Run the credential provider directly with the following command: `C:\Users\<user>\.nuget\plugins\netfx\CredentialProvider.Microsoft\CredentialProvider.Microsoft.exe  -I -V Verbose -U "https://pkgs.dev.azure.com/{organization}/{project-if-feed-is-project-scoped}/_packaging/{feed}/nuget/v3/index.json"`. Check you have the right permissions from the [feed permissions](https://docs.microsoft.com/en-us/azure/devops/artifacts/feeds/feed-permissions?view=azure-devops).

In an Azure DevOps Pipeline, verify you have set the right permissions for the pipeline by following the [docs](https://docs.microsoft.com/en-us/azure/devops/artifacts/feeds/feed-permissions?view=azure-devops#package-permissions-in-azure-pipelines).

#### Cred provider used to work but now it asks me to update the .NET version.
The .NET version installed is [out of support](https://dotnet.microsoft.com/platform/support/policy/dotnet-core#lifecycle), you should update to .NET 8.0 or greater to keep using the latest versions of the credential provider.

> .NET Core 3.1 and .NET Framework 4.6.1 compatability will also be removed from major version 2.0.0. See the announcement [here](https://github.com/microsoft/artifacts-credprovider/discussions/386).

To keep using .NET versions which are past their end of support date, see the table below for the maximum Artifacts Credential Provider version.

| .NET Version | End of ACP Support |
| -------- | ------- |
| .NET Core 2.1 | 0.1.28 |
| .NET Core 3.1 | 1.x.x (pending final release) |
| .NET Framework 4.6.1 | 1.x.x (pending final release) |
| .NET 6.0 | 2.x.x (pending release) |

## Contribute

This project welcomes contributions and suggestions; see [CONTRIBUTING](CONTRIBUTING.md) for more information.
Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

When submitting a pull request, please include a description of the problem your PR is trying to solve, details about the changes, and how the change was tested.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
