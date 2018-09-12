# A PowerShell script that adds the latest version of the Azure Artifacts credential provider
# plugin for Dotnet and/or NuGet to ~/.nuget/plugins directory
# To install netcore, run installcredprovider.ps1
# To install netcore and netfx, run installcredprovider.ps1 -AddNetfx
# More: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

param(
    [switch]$AddNetfx
)

$script:ErrorActionPreference='Stop'

# Without this, System.Net.WebClient.DownloadFile will fail on a client with TLS 1.0/1.1 disabled
if ([Net.ServicePointManager]::SecurityProtocol.ToString().Split(',').Trim() -notcontains 'Tls12') {
    [Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12
}

# Get the zip file from latest GitHub release
$latestReleaseUrl = "https://api.github.com/repos/Microsoft/artifacts-credprovider/releases/latest"
$latestRelease = Invoke-WebRequest -UseBasicParsing $latestReleaseUrl
try {
    $latestReleaseJson = $latestRelease.Content | ConvertFrom-Json
    $zipAsset = $latestReleaseJson.assets | ? { $_.content_type -eq "application/x-zip-compressed" }
    $packageSourceUrl = $zipAsset.browser_download_url
} catch {
    Write-Error "Unable to resolve the Credential Provider zip file from $latestReleaseUrl"
    return
}

if (!$packageSourceUrl) {
    Write-Error "Unable to resolve the Credential Provider zip file from $latestReleaseUrl"
    return
}

# Create temporary location for the zip file handling
$tempZipLocation = "$env:TEMP\CredProviderZip"
Write-Host "Creating temp directory for the Credential Provider zip: $tempZipLocation"
if (Test-Path -Path $tempZipLocation) {
    Remove-Item $tempZipLocation -Force -Recurse
}
New-Item -ItemType Directory -Force -Path $tempZipLocation

# Download credential provider zip to the temp location
$pluginZip = "$tempZipLocation\Microsoft.NuGet.CredentialProvider.zip"
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
$pluginLocation = "$env:USERPROFILE\.nuget\plugins"
Write-Host "Copying Credential Provider to $pluginLocation"
$localNetcoreCredProviderPath = "netcore\CredentialProvider.Microsoft"
Copy-Item "$tempZipLocation\plugins\$localNetcoreCredProviderPath" -Destination "$pluginLocation\$localNetcoreCredProviderPath" -Force -Recurse
if ($AddNetfx -eq $True) {
    $localNetfxCredProviderPath = "netfx\CredentialProvider.Microsoft"
    Copy-Item "$tempZipLocation\plugins\$localNetfxCredProviderPath" -Destination "$pluginLocation\$localNetfxCredProviderPath" -Force -Recurse
}

# Remove $tempZipLocation directory
Write-Host "Removing the Credential Provider temp directory $tempZipLocation"
Remove-Item $tempZipLocation -Force -Recurse

Write-Host "Credential Provider installed successfully"