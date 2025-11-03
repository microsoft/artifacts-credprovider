# Contributing to the Azure Artifacts Credential Provider

## Building

```shell
dotnet build CredentialProvider.Microsoft --configuration Release
```

In this and subsequent examples, configuration can be either `debug` or `release`.

## Developing

```shell
dotnet run --project CredentialProvider.Microsoft --framework net8.0 -- # args here

# Example: force-reload credentials for a known URL
dotnet run --project CredentialProvider.Microsoft --framework net8.0 -- -U https://foo.pkgs.visualstudio.com/Project/_packaging/MyProject_PublicPackages/nuget/v3/index.json -v Verbose -I
```

### Developing: test with NuGet, `dotnet`, and MSBuild

Set `$env:NUGET_PLUGIN_PATHS` (`%NUGET_PLUGIN_PATHS%`):

```pwsh
# Use the debug netcoreapp3.1 build
dotnet build CredentialProvider.Microsoft --configuration Debug
$env:NUGET_PLUGIN_PATHS="C:\Path\To\artifacts-credprovider\CredentialProvider.Microsoft\bin\Debug\netcoreapp3.1\CredentialProvider.Microsoft.exe"

# (Optional) To view logs, log to a file:
$env:NUGET_CREDENTIALPROVIDER_LOG_PATH="logs/test.log"

# Now real tools will use your build:
nuget search foo
dotnet package search foo
```

## Publishing

```shell
dotnet publish CredentialProvider.Microsoft --configuration Release --framework net8.0
```

## Packing

```shell
dotnet pack CredentialProvider.Microsoft --configuration Release
```

For CI builds, you can append a pre-release version:

```shell
dotnet pack CredentialProvider.Microsoft --configuration Release /p:NuspecProperties=VersionSuffix=MyCustomVersion-2
```

## Versioning

When releasing a new version, update the CredentialProviderVersion property in Build.props

## Common issues

> > nuget search foo
>
> The plugin at 'C:\Users\Me\Projects\artifacts-credprovider\CredentialProvider.Microsoft\bin\Release\net8.0\publish\CredentialProvider.Microsoft.dll' did not have a valid embedded signature.

You're on an old version of `nuget`. In a new terminal, run `nuget update -Self`.

See [the PR that removed AuthentiCode requirements](https://github.com/NuGet/NuGet.Client/pull/6042) & [relevant documentation bug](https://github.com/NuGet/Home/issues/13850).