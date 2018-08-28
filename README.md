# Azure Artifacts Credential Provider

The Azure Artifacts Credential Provider automates the acquisition of credentials needed to restore NuGet packages as part of your .NET development workflow. It integrates with MSBuild, dotnet, and NuGet(.exe) and works on Windows, Mac, and Linux. Any time you want to use packages from an Azure Artifacts feed, the Credential Provider will automatically acquire and securely store a token on behalf of the NuGet client you're using.

[![Build status](https://mseng.visualstudio.com/_apis/public/build/definitions/b924d696-3eae-4116-8443-9a18392d8544/7110/badge?branchName=master)](https://mseng.visualstudio.com/VSOnline/_build/latest?definitionId=7110&branch=master)

## Get

### MSBuild on Windows

Install the [15.9 preview of any Visual Studio edition](https://visualstudio.microsoft.com/vs/preview/), including the Build Tools edition. 

### All other clients (`dotnet`, `nuget`) on all platforms

Select, copy, and run the appropriate script for your shell/platform:

#### Shell (bash, zsh, etc.)

```shell
[command]
```

#### PowerShell 

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

Once you've successfully acquired a token, you can run authenticated commands without the `--interactive` flag.

### nuget

The NuGet client is interactive by default. To perform an operation that requires authentication, simply run it as expected. For example, to restore packages, navigate to your solution directory and run:

```shell
nuget restore
```

### msbuild

The first time you perform an operation that requires authentication using `msbuild`, you must use the `/p:nugetInteractive=true` switch to allow `msbuild` to prompt you for credentials. For example, to restore packages, navigate to your project or solution directory and run:

```shell
msbuild /t:restore /p:nugetInteractive=true
```

Once you've successfully acquired a token, you can run authenticated commands without the `/p:nugetInteractive=true` switch.

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

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
