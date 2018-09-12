# A PowerShell script that adds the latest version of the Azure Artifacts credential provider
# plugin for Dotnet and/or NuGet to ~/.nuget/plugins directory
# To install netcore, run installcredprovider.ps1
# To install netcore and netfx, run installcredprovider.ps1 -AddNetfx true
# More: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

param(
    $AddNetfx = $False
)

$script:ErrorActionPreference='Stop'

$nugetDirectory = "$env:USERPROFILE\.nuget"
$tempZipLocation = "$nugetDirectory\CredProviderZip"
$pluginLocation = "$nugetDirectory\plugins"
$localNetfxCredProviderPath = "netfx\CredentialProvider.Microsoft"
$localNetcoreCredProviderPath = "netcore\CredentialProvider.Microsoft"

# Get the zip file from latest GitHub release
$latestReleaseUrl = Invoke-WebRequest -UseBasicParsing "https://api.github.com/repos/Microsoft/artifacts-credprovider/releases/latest"
$latestReleaseJson = $latestReleaseUrl.Content | convertfrom-json
$zipAsset = $latestReleaseJson.assets | ? { $_.content_type -eq "application/x-zip-compressed" }
$packageSourceUrl = $zipAsset.browser_download_url
if (!$packageSourceUrl) {
    Write-host "Unable to resolve the Credential Provider zip file from $latestReleaseUrl"
    return
}

# Create temporary location for the zip file handling
Write-host "Creating temp directory for the Credential Provider zip: $tempZipLocation"
$tempLocationExists = Test-Path -Path $tempZipLocation
if ($tempLocationExists -eq $True) {
    Remove-Item $tempZipLocation -Force -Recurse
}
New-Item -ItemType Directory -Force -Path $tempZipLocation

# Download credential provider zip to the temp location
$pluginZip = "$tempZipLocation\Microsoft.NuGet.CredentialProvider.zip"
$client = New-Object System.Net.WebClient
Write-host "Downloading $packageSourceUrl to $pluginZip"
try {
    $client.DownloadFile($packageSourceUrl, $pluginZip)
} catch {
    Write-host "Unable to download $packageSourceUrl to the location $pluginZip"
}

# Extract zip to temp directory
Write-host "Extracting zip to the Credential Provider temp directory"
Add-Type -AssemblyName System.IO.Compression.FileSystem 
[System.IO.Compression.ZipFile]::ExtractToDirectory($pluginZip, $tempZipLocation)

# Forcibly copy netfx and netcore directories to plugins directory
Write-host "Copying Credential Provider to $pluginLocation"
Copy-Item "$tempZipLocation\plugins\$localNetfxCredProviderPath" -Destination "$pluginLocation\$localNetfxCredProviderPath" -Force -Recurse
Copy-Item "$tempZipLocation\plugins\$localNetcoreCredProviderPath" -Destination "$pluginLocation\$localNetcoreCredProviderPath" -Force -Recurse

# Remove $tempZipLocation directory
Write-host "Removing the Credential Provider temp directory $tempZipLocation"
Remove-Item $tempZipLocation -Force -Recurse

Write-host "Credential Provider installed successfully"
