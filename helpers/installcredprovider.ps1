# A PowerShell script that adds the latest version of the Azure Artifacts credential provider
# plugin for Dotnet and/or NuGet to ~/.nuget/plugins directory
# To install netcore, run installcredprovider.ps1
# To install netcore and netfx, run installcredprovider.ps1 -AddNetfx
# To overwrite existing plugin with the latest, run installcredprovider.ps1 -Force
# More: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

param(
    [switch]$AddNetfx,
    [switch]$Force
)

$script:ErrorActionPreference='Stop'

# Without this, System.Net.WebClient.DownloadFile will fail on a client with TLS 1.0/1.1 disabled
if ([Net.ServicePointManager]::SecurityProtocol.ToString().Split(',').Trim() -notcontains 'Tls12') {
    [Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12
}

$profilePath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile)
$tempPath = [System.IO.Path]::GetTempPath()

$pluginLocation = [System.IO.Path]::Combine($profilePath, ".nuget", "plugins");
$tempZipLocation = [System.IO.Path]::Combine($tempPath, "CredProviderZip");

$localNetcoreCredProviderPath = [System.IO.Path]::Combine("netcore", "CredentialProvider.Microsoft");
$localNetfxCredProviderPath = [System.IO.Path]::Combine("netfx", "CredentialProvider.Microsoft");

# Check if plugin already exists if -Force swich is not set
if (!$Force) {
    $netfxExists = Test-Path -Path ([System.IO.Path]::Combine($pluginLocation, $localNetfxCredProviderPath))
    $netcoreExists = Test-Path -Path ([System.IO.Path]::Combine($pluginLocation, $localNetcoreCredProviderPath))
    if ($AddNetfx -eq $True -and $netfxExists -eq $True -and $netcoreExists -eq $True) {
        Write-Host "The netcore and netfx Credential Providers are already in $pluginLocation"
        return
    }

    if ($AddNetfx -eq $False -and $netcoreExists -eq $True) {
        Write-Host "The netcore Credential Provider is already in $pluginLocation"
        return
    }
}

# Get the zip file from latest GitHub release
$latestReleaseUrl = "https://api.github.com/repos/Microsoft/artifacts-credprovider/releases/latest"
$latestRelease = Invoke-WebRequest -UseBasicParsing $latestReleaseUrl
$zipErrorString = "Unable to resolve the Credential Provider zip file from $latestReleaseUrl"
try {
    $latestReleaseJson = $latestRelease.Content | ConvertFrom-Json
    if ($AddNetfx -eq $True) {
        Write-Host "Using Microsoft.NuGet.CredentialProvider.zip"
        $zipAsset = $latestReleaseJson.assets | ? { $_.name -eq "Microsoft.NuGet.CredentialProvider.zip" }
    } else {
        Write-Host "Using Microsoft.NetCore2.NuGet.CredentialProvider.zip"
        $zipAsset = $latestReleaseJson.assets | ? { $_.name -eq "Microsoft.NetCore2.NuGet.CredentialProvider.zip" }
    }
    
    $packageSourceUrl = $zipAsset.browser_download_url
} catch {
    Write-Error $zipErrorString
    return
}

if (!$packageSourceUrl) {
    Write-Error $zipErrorString
    return
}

# Create temporary location for the zip file handling
Write-Host "Creating temp directory for the Credential Provider zip: $tempZipLocation"
if (Test-Path -Path $tempZipLocation) {
    Remove-Item $tempZipLocation -Force -Recurse
}
New-Item -ItemType Directory -Force -Path $tempZipLocation

# Download credential provider zip to the temp location
$pluginZip = ([System.IO.Path]::Combine($tempZipLocation, "Microsoft.NuGet.CredentialProvider.zip"))
Write-Host "Downloading $packageSourceUrl to $pluginZip"
try {
    $client = New-Object System.Net.WebClient
    $client.DownloadFile($packageSourceUrl, $pluginZip)
} catch {
    Write-Error "Unable to download $packageSourceUrl to the location $pluginZip"
}

# Extract zip to temp directory
Write-Host "Extracting zip to the Credential Provider temp directory"
Add-Type -AssemblyName System.IO.Compression.FileSystem 
[System.IO.Compression.ZipFile]::ExtractToDirectory($pluginZip, $tempZipLocation)

# Forcibly copy netcore (and netfx) directories to plugins directory
Write-Host "Copying Credential Provider to $pluginLocation"
Copy-Item ([System.IO.Path]::Combine($tempZipLocation, "plugins", $localNetcoreCredProviderPath)) -Destination ([System.IO.Path]::Combine($pluginLocation, $localNetcoreCredProviderPath)) -Force -Recurse
if ($AddNetfx -eq $True) {
    Copy-Item ([System.IO.Path]::Combine($tempZipLocation, "plugins", $localNetfxCredProviderPath)) -Destination ([System.IO.Path]::Combine($pluginLocation, $localNetfxCredProviderPath)) -Force -Recurse
}

# Remove $tempZipLocation directory
Write-Host "Removing the Credential Provider temp directory $tempZipLocation"
Remove-Item $tempZipLocation -Force -Recurse

Write-Host "Credential Provider installed successfully"
