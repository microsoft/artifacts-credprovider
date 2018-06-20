# MicrosoftCredentialProvider

## Status
| | Build & Test |
|-|--------------|
| **Windows** | [![Build status](https://mseng.visualstudio.com/_apis/public/build/definitions/b924d696-3eae-4116-8443-9a18392d8544/7110/badge)](https://mseng.visualstudio.com/VSOnline/_build/latest?definitionId=7110) |

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

# MicrosoftCredentialProvider VSIX
## Building
Build the solution in Visual Studio 2017, or using msbuild:
```
msbuild MicrosoftCredentialProvider.sln /p:Configuration=Release /t:restore,build
```

# Versioning
Update the following files when modifying the version:
- CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj
- CredentialProvider.Microsoft\CredentialProvider.Microsoft.nuspec
- MicrosoftCredentialProviderVSIX\bin\Debug\extension.vsixmanifest

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
