<#
.SYNOPSIS
    Executes a versioned installation script of artifacts-credprovider from a GitHub release artifact.

.DESCRIPTION
    This script downloads a file from a GitHub artifacts-credprovider release artifact using the provided
    version string and executes the downloaded script, allowing the install script parameters to be passed.

.PARAMETER Version
    The version string of the release (e.g., "1.4.1").

.EXAMPLE
    .\installcredproviderrelease.ps1 -Version 1.3.0 -AddNetFx -Force"
#>

[CmdletBinding(HelpUri = "https://github.com/microsoft/artifacts-credprovider/blob/master/README.md#setup")]
param(
    [Parameter(Mandatory = $false)]
    [string]$Version,

    [Parameter(Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string]$AdditionalParams
)

$ErrorActionPreference = 'Stop'

function Get-ReleaseUrl {
    # Get the file base URL from the GitHub release
    $releaseUrlBase = "https://api.github.com/repos/microsoft/artifacts-credprovider/releases"
    $versionError = "Unable to find the release version $ReleaseVersion from $releaseUrlBase"
    $releaseId = "latest"
    if (![string]::IsNullOrEmpty($ReleaseVersion)) {
        try {
            $releases = Invoke-WebRequest -UseBasicParsing $releaseUrlBase
            $releaseJson = $releases | ConvertFrom-Json
            $correctReleaseVersion = $releaseJson | ? { $_.name -eq $ReleaseVersion }
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

    $releaseUrl = [System.IO.Path]::Combine($releaseUrlBase, $releaseId)
    return $releaseUrl.Replace("\", "/")
}

# Version parameter validation
if ($Version) {
    if ($Version -match '^[vV]') {
        $Version = $Version.Substring(1) # Remove leading 'v' or 'V'
    }

    if ($Version -notmatch '^\d+\.\d+\.\d+') {
        Write-Error "Invalid version. Please use the format #.#.# to override the release version."
        return
    }
}

# Construct the GitHub release URL
if ($Version -and ($Version.StartsWith("0.") -or $Version.StartsWith("1."))) {
    # For versions 0.x and 1.0.x, use the last 1.x release URL without sha256 validation
    # for backward compatibility
    $ReleaseVersion = "1.4.1"
    $releaseUrl = Get-ReleaseUrl
}
else {
    $ReleaseVersion = $Version
    $releaseUrl = Get-ReleaseUrl
    $checksumUrl = "$releaseUrl/artifacts-credprovider-sha256.txt"
}

$installScriptName = "installcredprovider.ps1"
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
    $installAsset = $releaseJson.assets | ? { $_.name -eq $installScriptName }
    if (!$installAsset) {
        throw ("Unable to find asset $installScriptName from release JSON object")
    }
    $installUrl = $installAsset.browser_download_url
    if (!$installUrl) {
        throw ("Unable to find download url from asset $installAsset")
    }
}
catch {
    Write-Error ("Unable to resolve the browser download url from $releaseUrl `nError: " + $_.Exception.Message)
    return
}

# Build the parameters for the executed install script
$paramString = ""
if ($Version) {
    $paramString += "-Version $Version "
}
if ($AdditionalParams) {
    $paramString += $AdditionalParams
}

# Fetch the checksum file and validate the hash
try {
    if ($null -ne $checksumUrl) {
        # If the checksum file exists locally, read it with UTF-8 encoding
        $localChecksumPath = "C:\Users\coallred\Downloads\TestHash\artifacts-credprovider-sha256.txt"
        if (Test-Path $localChecksumPath) {
            $checksumContent = [System.IO.File]::ReadAllText($localChecksumPath, [System.Text.Encoding]::UTF8)
        }
        else {
            # Fetch from remote as currently implemented
            Write-Host "Fetching checksum file from $checksumUrl..."
            $response = Invoke-WebRequest -Uri $checksumUrl -UseBasicParsing
            $checksumContent = $response.Content
        }

        # Extract the expected hash from the checksum content by splitting by newline and finding the entry for installcredprovider.ps1
        $checksumLines = $checksumContent -split '\r?\n'
        $matchingLine = $checksumLines | Where-Object { $_ -match $installScriptName }
        
        if ($matchingLine) {
            # Extract the hash from the matching line which are formatted as "filename hash"
            $parts = $matchingLine -split '\s+'
            $expectedHash = $parts[1]
        }
        else {
            Write-Warning "Could not find hash for $installScriptName in artifacts-credprovider-sha256.txt. Proceeding without validation."
        }
    }

    # Fetch the install file content
    Write-Host "Fetching $installScriptName from $installUrl..."
    $tempScriptLocation = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), $installScriptName);
    try {
        $client = New-Object System.Net.WebClient
        $client.DownloadFile($installUrl, $tempScriptLocation)
    }
    catch {
        Write-Error "Unable to download $packageSourceUrl to the location $pluginZip"
    }

    if ($null -ne $expectedHash) {
        $actualHash = (Get-FileHash -Path $tempScriptLocation -Algorithm SHA256).Hash
        if ($actualHash.ToLower() -ne $expectedHash.ToLower()) {
            throw "The downloaded $installScriptName hash does not match. `nExpected: $expectedHash, Actual: $actualHash"
        }
    }

    # Execute the script directly from the URL with additional parameters
    Write-Host "Executing $installScriptName..."
    $execCmd = "& { $($tempScriptLocation) $paramString }"
    Write-Host "Executing command: $execCmd`n"
    Invoke-Expression -Command $execCmd
}
catch {
    Write-Error "Failed to fetch, validate, or execute artifacts-credprovider install. Error message: $_"
}
