# MicrosoftCredentialProvider

## Status
|                    | Build & Test | MSCredProvider |
|--------------------|--------------|----------------|
| **Windows**        |[![Build status](https://mseng.visualstudio.com/_apis/public/build/definitions/b924d696-3eae-4116-8443-9a18392d8544/7110/badge?branchName=master)](https://mseng.visualstudio.com/VSOnline/_build/latest?definitionId=7110&branch=master)| [![Microsoft.NuGet.CredentialProvider package in MSCredProvider feed in Visual Studio Team Services](https://mseng.feeds.visualstudio.com/_apis/public/Packaging/Feeds/54754426-96db-4f6e-8a3a-64265d1cc147/Packages/16200823-3f36-4334-a4ec-7b7b6cd5243d/Badge)](https://mseng.visualstudio.com/_Packaging?feed=54754426-96db-4f6e-8a3a-64265d1cc147&package=16200823-3f36-4334-a4ec-7b7b6cd5243d&preferRelease=true&_a=package) |

The configuration parameter in the examples below can be either Debug or Release

## Building
```
dotnet build CredentialProvider.Microsoft --configuration Release
```

## Packing
```
dotnet pack CredentialProvider.Microsoft --configuration Release /p:NuspecFile=CredentialProvider.Microsoft.nuspec
```
For CI builds, you can append a pre-release version
```
dotnet pack CredentialProvider.Microsoft --configuration Release /p:NuspecFile=CredentialProvider.Microsoft.nuspec /p:NuspecProperties=VersionSuffix=-MyCustomVersion-2
```

# Versioning
Update the following files when modifying the version:
- CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj
- CredentialProvider.Microsoft\CredentialProvider.Microsoft.nuspec
- CredentialProvider.Microsoft.VSIX\Microsoft.CredentialProvider.swixproj
- CredentialProvider.Microsoft.VSIX\Microsoft.CredentialProvider.swr

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
