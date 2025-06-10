<#
.SYNOPSIS
    Installs the Azure Artifacts Credential Provider for DotNet or NuGet tool usage.

.DESCRIPTION
    This script installs the latest version of the Azure Artifacts Credential Provider plugin
    for DotNet and/or NuGet to the ~/.nuget/plugins directory.

.PARAMETER AddNetfx
    Installs the .NET Framework 4.8.1 Credential Provider.
    For backwards compatability, this is equivalent to -AddNetfx48.

.PARAMETER AddNetfx48
    Installs the .NET Framework 4.8.1 Credential Provider.

.PARAMETER Force
    Forces overwriting of existing Credential Provider installations.

.PARAMETER Version
    Specifies the GitHub release version of the Credential Provider to install.

.PARAMETER InstallNet6
    Installs the .NET 6 Credential Provider.

.PARAMETER InstallNet8
    Installs the .NET 8 Credential Provider (default).

.PARAMETER RuntimeIdentifier
    Installs the self-contained Credential Provider for the specified Runtime Identifier.

.EXAMPLE
    .\installcredprovider.ps1 -InstallNet8 -AddNetfx
    .\installcredprovider.ps1 -Version "2.0.1" -Force
    .\installcredprovider.ps1 -RuntimeIdentifier "osx-x64" -Force
#>

[CmdletBinding(HelpUri = "https://github.com/microsoft/artifacts-credprovider/blob/master/README.md#setup")]
param(
    [switch]$AddNetfx,
    [switch]$AddNetfx48,
    [switch]$Force,
    [string]$Version,
    [switch]$InstallNet6,
    [switch]$InstallNet8 = $true,
    [string]$RuntimeIdentifier
)

$script:ErrorActionPreference = 'Stop'

function Initialize-InstallParameters {
    # Start with invalid parameter checks
    if (![string]::IsNullOrEmpty($Version)) {
        if ($Version -notmatch '^\d+\.\d+\.\d+') {
            Write-Error "Invalid version format specified. Please use the format #.#.# to override the release version."
            return
        }
    }

    # Check if the version is valid given the install options
    if (![string]::IsNullOrEmpty($RuntimeIdentifier)) {
        Write-Host "RuntimeIdentifier parameter is specified, the $RuntimeIdentifier self-contained version will be installed"
        $InstallNet6 = $False
        $InstallNet8 = $True
    }
    if ($InstallNet6 -eq $True -and $InstallNet8 -eq $True) {
        # If .NET 6 and 8 are specified, .NET 8 will be installed
        $InstallNet6 = $False
    }
    if ($AddNetfx48 -eq $True) {
        # For backward compatibility, AddNetfx and AddNetfx48 are equivalent
        $AddNetfx = $True
    }
}

function Get-RuntimeIdentifier {
    if ($IsLinux) {
        $runtimeId = "linux"
    }
    elseif ($IsMacOS) {
        $runtimeId = "osx"
    }
    elseif ($IsWindows) {
        $runtimeId = "win"
    }
    else {
        Write-Warning "Unable to automatically detect a supported OS. The .NET 8 version will be installed by default. Please set the RuntimeIdentifier parameter to specify a runtime version."
        return ""
    }

    $osArch = (Get-CimInstance Win32_Processor).Architecture
    switch ($osArch) {
        9 { $osArch = "-x64" }
        12 { $osArch = "-arm64" }
        default {
            Write-Warning "Unable to automatically detect a supported CPU architecture. The .NET 8 version will be installed by default. Please set the RuntimeIdentifier parameter to specify a runtime version."
            return ""
        }
    }

    # Windows on ARM64 runs x64 binaries
    if ($osArch -eq "-arm64" -and $IsWindows) {
        $runtimeId += "-x64"  
    }
    else {
        $runtimeId += $osArch
    }

    Write-Verbose "Calculated artifacts-credprovider RuntimeIdentifier: $runtimeId"
    return "$runtimeId."
}

function Get-ReleaseUrl {
    # Get the file base URL from the GitHub release
    $releaseUrlBase = "https://api.github.com/repos/Microsoft/artifacts-credprovider/releases"
    $versionError = "Unable to find the release version $Version from $releaseUrlBase"
    $releaseId = "latest"
    if (![string]::IsNullOrEmpty($Version)) {
        try {
            $releases = Invoke-WebRequest -UseBasicParsing $releaseUrlBase
            $releaseJson = $releases | ConvertFrom-Json
            $correctReleaseVersion = $releaseJson | ? { $_.name -eq $Version }
            $releaseId = $correctReleaseVersion.id
        }
        catch {
            Write-Error $versionError
            return
        }
    }

    if (!$releaseId) {
        Write-Error $versionError
        return
    }

    $releaseUrlId = [System.IO.Path]::Combine($releaseUrlBase, $releaseId)
    return $releaseUrlId.Replace("\", "/")
}

function Install-CredProvider {
    Write-Verbose "Using $archiveFile"

    try {
        Write-Host "Fetching release $releaseUrl"
        $release = Invoke-WebRequest -UseBasicParsing $releaseUrl
        if (!$release) {
            throw ("Unable to make Web Request to $releaseUrl")
        }
        $releaseJson = $release.Content | ConvertFrom-Json
        if (!$releaseJson) {
            throw ("Unable to get content from JSON")
        }
        $archiveAsset = $releaseJson.assets | ? { $_.name -eq $archiveFile }
        if (!$archiveAsset) {
            throw ("Unable to find asset $archiveFile from release json object")
        }
        $packageSourceUrl = $archiveAsset.browser_download_url
        if (!$packageSourceUrl) {
            throw ("Unable to find download url from asset $archiveAsset")
        }
    }
    catch {
        Write-Error ("Unable to resolve the browser download url from $releaseUrl `nError: " + $_.Exception.Message)
        return
    }

    # Create temporary location for the zip file handling
    Write-Verbose "Creating temp directory for the Credential Provider zip: $tempZipLocation"
    if (Test-Path -Path $tempZipLocation) {
        Remove-Item $tempZipLocation -Force -Recurse
    }
    New-Item -ItemType Directory -Force -Path $tempZipLocation

    # Download credential provider zip to the temp location
    $pluginZip = ([System.IO.Path]::Combine($tempZipLocation, $archiveFile))
    Write-Host "Downloading $packageSourceUrl to $pluginZip"
    try {
        $client = New-Object System.Net.WebClient
        $client.DownloadFile($packageSourceUrl, $pluginZip)
    }
    catch {
        Write-Error "Unable to download $packageSourceUrl to the location $pluginZip"
    }

    # Extract zip to temp directory
    Write-Host "Extracting zip to the Credential Provider temp directory $tempZipLocation"
    # Add-Type -AssemblyName System.IO.Compression.FileSystem
    if ($archiveFile -like "*.tar.gz") {
        # Extract .tar.gz using tar, available on Windows 10 and later
        Write-Host "Extracting tar.gz archive $pluginZip to $tempZipLocation"
        tar -xzf $pluginZip -C $tempZipLocation
    }
    else {
        # Extract .zip using Expand-Archive
        Expand-Archive -Path $pluginZip -DestinationPath $tempZipLocation -Force
    }
}

# Without this, System.Net.WebClient.DownloadFile will fail on a client with TLS 1.0/1.1 disabled
if ([Net.ServicePointManager]::SecurityProtocol.ToString().Split(',').Trim() -notcontains 'Tls12') {
    [Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12
}

# Run script parameter validation
Initialize-InstallParameters

$userProfilePath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile);
if ($userProfilePath -ne '') {
    $profilePath = $userProfilePath
}
else {
    $profilePath = $env:UserProfile
}

$tempPath = [System.IO.Path]::GetTempPath()

$pluginLocation = [System.IO.Path]::Combine($profilePath, ".nuget", "plugins");
$tempZipLocation = [System.IO.Path]::Combine($tempPath, "CredProviderZip");

$localNetcoreCredProviderPath = [System.IO.Path]::Combine("netcore", "CredentialProvider.Microsoft");
$localNetfxCredProviderPath = [System.IO.Path]::Combine("netfx", "CredentialProvider.Microsoft");

$fullNetfxCredProviderPath = [System.IO.Path]::Combine($pluginLocation, $localNetfxCredProviderPath)
$fullNetcoreCredProviderPath = [System.IO.Path]::Combine($pluginLocation, $localNetcoreCredProviderPath)

$netfxExists = Test-Path -Path ($fullNetfxCredProviderPath)
$netcoreExists = Test-Path -Path ($fullNetcoreCredProviderPath)

# Check if plugin already exists if -Force swich is not set
if (!$Force) {
    if ($AddNetfx -eq $True -and $netfxExists -eq $True) {
        Write-Host "The netfx Credential Providers are already in $pluginLocation. Please use -Force to overwrite."
        return
    }

    if (($InstallNet6 -eq $True -or $InstallNet8 -eq $True) -and $netcoreExists -eq $True) {
        Write-Host "The netcore Credential Provider is already in $pluginLocation. Please use -Force to overwrite."
        return
    }
}

$releaseUrl = Get-ReleaseUrl

if ([string]::IsNullOrEmpty($RuntimeIdentifier)) {
    $releaseRidPart = Get-RuntimeIdentifier
}
else {
    $releaseRidPart = "$RuntimeIdentifier."
}
Write-Host "Using RuntimeIdentifier: $releaseRidPart"

if ($InstallNet6 -eq $True) {
    $archiveFile = "Microsoft.Net6.NuGet.CredentialProvider.zip"
}
if ($InstallNet8 -eq $True) {
    if ($releaseRidPart -like 'linux*') {
        # For linux runtimes, only .tar.gz is available
        $archiveFile = "Microsoft.Net8.${releaseRidPart}NuGet.CredentialProvider.tar.gz"
    }
    else {
        $archiveFile = "Microsoft.Net8.${releaseRidPart}NuGet.CredentialProvider.zip"
    }
}
if ($AddNetfx -eq $True) {
    # This conditional must come last as two downloads occur when NetFx/Core are installed
    $archiveFile = "Microsoft.NetFx48.NuGet.CredentialProvider.zip"
}

# Call Install-CredProvider function
Install-CredProvider

# Remove existing content and copy netfx directories to plugins directory
if ($AddNetfx -eq $True) {
    if ($netfxExists) {
        Write-Verbose "Removing existing content from $fullNetfxCredProviderPath"
        Remove-Item $fullNetfxCredProviderPath -Force -Recurse
    }
    $tempNetfxPath = [System.IO.Path]::Combine($tempZipLocation, "plugins", $localNetfxCredProviderPath)
    Write-Verbose "Copying Credential Provider from $tempNetfxPath to $fullNetfxCredProviderPath"
    Copy-Item $tempNetfxPath -Destination $fullNetfxCredProviderPath -Force -Recurse
}

# Microsoft.NuGet.CredentialProvider.zip that installs netfx provider installs .netcore3.1 version
# Also install NET6/NET8 provider if requested
if ($AddNetfx -eq $True -and $InstallNet6 -eq $True) {
    $archiveFile = "Microsoft.Net6.NuGet.CredentialProvider.zip"
    Write-Verbose "Installing Net6"
    Install-CredProvider
}
if ($AddNetfx -eq $True -and $InstallNet8 -eq $True) {
    if ($releaseRidPart -like 'linux*') {
        # For linux runtimes, only .tar.gz is available
        $archiveFile = "Microsoft.Net8.${releaseRidPart}NuGet.CredentialProvider.tar.gz"
    }
    else {
        $archiveFile = "Microsoft.Net8.${releaseRidPart}NuGet.CredentialProvider.zip"
    }
    Write-Verbose "Installing Net8"
    Install-CredProvider
}

# Remove existing content and copy netcore directories to plugins directory
if ($netcoreExists) {
    Write-Verbose "Removing existing content from $fullNetcoreCredProviderPath"
    Remove-Item $fullNetcoreCredProviderPath -Force -Recurse
}
$tempNetcorePath = [System.IO.Path]::Combine($tempZipLocation, "plugins", $localNetcoreCredProviderPath)
Write-Verbose "Copying Credential Provider from $tempNetcorePath to $fullNetcoreCredProviderPath"
Copy-Item $tempNetcorePath -Destination $fullNetcoreCredProviderPath -Force -Recurse

# Remove $tempZipLocation directory
Write-Verbose "Removing the Credential Provider temp directory $tempZipLocation"
Remove-Item $tempZipLocation -Force -Recurse

Write-Host "Credential Provider installed successfully"
