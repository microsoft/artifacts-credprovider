# Contributing to the Azure Artifacts Credential Provider

### Building

```shell
dotnet build CredentialProvider.Microsoft --configuration Release
```

In this and subsequent examples, configuration can be either `debug` or `release`.

### Publishing

```shell
dotnet publish CredentialProvider.Microsoft --configuration Release --framework netcoreapp3.1
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