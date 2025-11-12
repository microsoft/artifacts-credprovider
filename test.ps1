#!/usr/bin/env pwsh

# A Windows domain user should be able to run this against a feed in an AAD-back AzDO org
# and all scenarios should succeed non-interactively.

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$TestFeed
)

if ([string]::IsNullOrEmpty($TestFeed)) {
    Write-Host "Please specify an AzDO organization package feed URL as the first parameter." -ForegroundColor Red
    exit 1
}

$env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED = "true"
$env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION = Join-Path ([System.IO.Path]::GetTempPath()) "msal.cache"

if (Test-Path $env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION) {
    Remove-Item $env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION -Force
}

function Test-Frameworks {
    param(
        [string]$TestFeedUrl,
        [switch]$Legacy
    )
    
    if ($IsWindows) {
        $frameworks = @("net481", "net6.0", "net8.0")
    } else {
        $frameworks = @("net6.0", "net8.0")
    }
    
    foreach ($framework in $frameworks) {
        # Clean up credential cache files
        $credProviderPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "MicrosoftCredentialProvider" "*.dat"
        if (Test-Path $credProviderPath) {
            Write-Host "Removing credential cache files at $credProviderPath" -ForegroundColor Yellow
            Remove-Item $credProviderPath -Force -ErrorAction SilentlyContinue
        }

        $withbroker = ($env:NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER -eq "true") -or ($env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER -eq "true")
        $entra_optin = ($env:ARTIFACTS_CREDENTIALPROVIDER_RETURN_ENTRA_TOKENS -eq "true")
        
        if ($Legacy) {
            if (Test-Path $env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION) {
                Write-Host "Removing MSAL file cache at $env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION" -ForegroundColor Yellow
                Remove-Item $env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION -Force -ErrorAction SilentlyContinue
            }

            Write-Host "Testing $framework with legacy envvars, broker=$withbroker entra_optin=$entra_optin" -ForegroundColor Cyan
        } else {
            if (Test-Path $env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION) {
                Write-Host "Removing MSAL file cache at $env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION" -ForegroundColor Yellow
                Remove-Item $env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION -Force -ErrorAction SilentlyContinue
            }

            Write-Host "Testing $framework with new envvars, broker=$withbroker entra_optin=$entra_optin" -ForegroundColor Cyan
        }

        $logFile = "test.$framework"

        if ($Legacy) {
            $logFile += ".legacy"
        } else {
            $logFile += ".new"
        }

        if ($withbroker) {
            $logFile += ".withbroker"
        } else {
            $logFile += ".nobroker"
        }
        if ($entra_optin) {
            $logFile += ".entratoken"
        }
        $logFile = "$logFile.log"
        
        $projectPath = Join-Path "CredentialProvider.Microsoft" "CredentialProvider.Microsoft.csproj"
        $command = "dotnet run --no-restore --no-build -f $framework --project $projectPath -- -C -U $TestFeedUrl -V Debug"
        Write-Host $command -ForegroundColor Gray
        
        # Execute the command and redirect output to log file
        try {
            $output = & dotnet run --no-restore --no-build -f $framework --project $projectPath -- -C -U $TestFeedUrl -V Debug 2>&1 | Out-File -FilePath $logFile -Encoding UTF8
            $exitCode = $LASTEXITCODE
        }
        catch {
            $exitCode = 1
            Write-Host "Command execution failed: $_" -ForegroundColor Red
        }
        
        if ($exitCode -ne 0) {
            Write-Host "Previous command execution failed: $exitCode" -ForegroundColor Red
            # Run again without redirection to show output
            & dotnet run --no-restore --no-build -f $framework --project $projectPath -- -C -U $TestFeedUrl -V Debug
            return $exitCode
        }
    }
    
    return 0
}

Write-Host "==== Testing with legacy NUGET_CREDENTIALPROVIDER_* environment variables ===="

# Unset new variables
$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED = $null
$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION = $null
$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER = $null

Write-Host "Testing MSAL without broker" -ForegroundColor Green

$env:NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER = "false"
$env:ARTIFACTS_CREDENTIALPROVIDER_RETURN_ENTRA_TOKENS = "false"
$result = Test-Frameworks -TestFeedUrl $TestFeed -Legacy
if ($result -ne 0) {
    Write-Host "Failed: $result" -ForegroundColor Red
    exit $result
}

Write-Host "==== Testing with new ARTIFACTS_CREDENTIALPROVIDER_* environment variables ===="

# Unset legacy variables
$env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED = $null
$env:NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION = $null
$env:NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER = $null

$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED = "true"
$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION = Join-Path ([System.IO.Path]::GetTempPath()) "msal.cache"
if (Test-Path $env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION) {
    Remove-Item $env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION -Force
}

Write-Host "Testing MSAL with Entra Token Opt-in" -ForegroundColor Green
$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER = "true"
$env:ARTIFACTS_CREDENTIALPROVIDER_RETURN_ENTRA_TOKENS = "true"
$result = Test-Frameworks -TestFeedUrl $TestFeed
if ($result -ne 0) {
    Write-Host "Failed: $result" -ForegroundColor Red
    exit $result
}

Write-Host "Testing MSAL without broker" -ForegroundColor Green
$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER = "false"
$env:ARTIFACTS_CREDENTIALPROVIDER_RETURN_ENTRA_TOKENS = "false"
$result = Test-Frameworks -TestFeedUrl $TestFeed
if ($result -ne 0) {
    Write-Host "Failed: $result" -ForegroundColor Red
    exit $result
}

Write-Host "Testing MSAL with broker" -ForegroundColor Green
$env:ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER = "true"
$env:ARTIFACTS_CREDENTIALPROVIDER_RETURN_ENTRA_TOKENS = "false"
$result = Test-Frameworks -TestFeedUrl $TestFeed
if ($result -ne 0) {
    Write-Host "Failed: $result" -ForegroundColor Red
    exit $result
}

Write-Host "All tests passed!" -ForegroundColor Green
exit 0