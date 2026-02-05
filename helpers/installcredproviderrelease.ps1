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
    $AdditionalParams
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

if ($Version -and ($Version.StartsWith("0.") -or $Version.StartsWith("1."))) {
    # For versions 0.x and 1.0.x, use the last 1.x release URL for backward compatibility
    $ReleaseVersion = "1.4.1"
    $releaseUrl = Get-ReleaseUrl
}
else {
    $ReleaseVersion = $Version
    $releaseUrl = Get-ReleaseUrl
}

$installScriptName = "installcredprovider.ps1"
try {
    Write-Host "Fetching release metadata from $releaseUrl"
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
    $installHash = $installAsset.digest
    if ($installHash -and $installHash.StartsWith("sha256:")) {
        $expectedHash = $installHash.Substring(7) # Remove "sha256:" prefix
    }
    else {
        $expectedHash = $null
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
$scriptArgs = @()
if ($Version) {
    $scriptArgs += "-Version"
    $scriptArgs += $Version
}
if ($AdditionalParams) {
    # Recombine arguments that were split by PowerShell's parser (e.g., "-Switch:" and "False")
    for ($i = 0; $i -lt $AdditionalParams.Count; $i++) {
        $arg = $AdditionalParams[$i]
        if ($arg -is [string] -and $arg.EndsWith(':') -and ($i + 1) -lt $AdditionalParams.Count) {
            # Combine "-Switch:" with its value (e.g., "-AddNetfx:" + "False" -> "-AddNetfx:False")
            $scriptArgs += "$arg$($AdditionalParams[$i + 1])"
            $i++
        } else {
            $scriptArgs += $arg
        }
    }
}
$paramString = $scriptArgs -join ' '

try {
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

     # Execute the script with parameters
    Write-Host "Executing $installScriptName..."
    Invoke-Expression "& '$tempScriptLocation' $paramString"
}
catch {
    Write-Error "Failed to fetch, validate, or execute artifacts-credprovider install. Error message: $_"
}
