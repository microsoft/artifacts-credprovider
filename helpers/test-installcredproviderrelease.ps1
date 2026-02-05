<#
.SYNOPSIS
    Integration tests for installcredproviderrelease.ps1

.DESCRIPTION
    These tests invoke installcredproviderrelease.ps1 with mocked network calls
    to verify that parameters are correctly passed to the downloaded install script.

.PARAMETER RunLive
    Actually runs against GitHub without mocking.
    WARNING: This will install the credential provider.

.EXAMPLE
    .\test-installcredproviderrelease.ps1
    .\test-installcredproviderrelease.ps1 -RunLive
#>

param(
    [switch]$RunLive
)

$ErrorActionPreference = 'Stop'
$script:TestsPassed = 0
$script:TestsFailed = 0

Write-Host "PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor Cyan

$script:ScriptUnderTest = Join-Path $PSScriptRoot "installcredproviderrelease.ps1"
$script:MockScriptPath = Join-Path ([System.IO.Path]::GetTempPath()) "installcredprovider.ps1"
$script:CaptureFilePath = Join-Path ([System.IO.Path]::GetTempPath()) "test-captured-params.json"

function Write-TestResult {
    param([string]$TestName, [bool]$Passed, [string]$Message = "")
    
    if ($Passed) {
        Write-Host "[PASS] $TestName" -ForegroundColor Green
        $script:TestsPassed++
    }
    else {
        Write-Host "[FAIL] $TestName" -ForegroundColor Red
        if ($Message) { Write-Host "       $Message" -ForegroundColor Red }
        $script:TestsFailed++
    }
}

function Initialize-MockScript {
    $captureFile = $script:CaptureFilePath
    @"
param(
    [switch]`$AddNetfx,
    [switch]`$AddNetfx48,
    [switch]`$Force,
    [string]`$Version,
    [switch]`$InstallNet6,
    [switch]`$InstallNet8,
    [switch]`$NonSelfContained,
    [string]`$RuntimeIdentifier,
    [switch]`$SomeNewSwitch,
    [string]`$SomeNewParam
)
@{
    AddNetfx = `$AddNetfx.IsPresent
    AddNetfx48 = `$AddNetfx48.IsPresent
    Force = `$Force.IsPresent
    Version = `$Version
    InstallNet6 = `$InstallNet6.IsPresent
    InstallNet8 = `$InstallNet8.IsPresent
    NonSelfContained = `$NonSelfContained.IsPresent
    RuntimeIdentifier = `$RuntimeIdentifier
    SomeNewSwitch = `$SomeNewSwitch.IsPresent
    SomeNewParam = `$SomeNewParam
} | ConvertTo-Json | Out-File '$captureFile' -Encoding utf8
"@ | Out-File $script:MockScriptPath -Encoding utf8 -Force
}

function Test-Parameters {
    param(
        [string]$TestName,
        [string]$ArgumentString,     # Raw argument string to pass
        [hashtable]$ExpectedParams   # Expected values in captured output
    )
    
    try {
        if ($RunLive) {
            Write-Host "  Live test: $TestName" -ForegroundColor Yellow
            $cmd = "& '$($script:ScriptUnderTest)' $ArgumentString"
            Invoke-Expression $cmd 2>&1 | Out-Null
            Write-TestResult -TestName $TestName -Passed $true
            return
        }
        
        # Clean up
        if (Test-Path $script:CaptureFilePath) { Remove-Item $script:CaptureFilePath -Force }
        Initialize-MockScript
        
        # Build test wrapper that mocks network and runs real script
        $scriptPath = $script:ScriptUnderTest -replace "'", "''"
        # Escape $ in argument string to prevent interpolation in here-string
        $escapedArgs = $ArgumentString -replace '\$', '`$'
        
        $wrapper = @"
`$ErrorActionPreference = 'Stop'
function Invoke-WebRequest {
    param([string]`$Uri, [switch]`$UseBasicParsing)
    if (`$Uri -match '/releases$') {
        return @(@{name='2.0.0';id=1},@{name='1.4.1';id=2}) | ConvertTo-Json
    }
    return [PSCustomObject]@{Content=(@{assets=@(@{name='installcredprovider.ps1';browser_download_url='fake';digest=`$null})}|ConvertTo-Json)}
}
`$orig = Get-Command New-Object -CommandType Cmdlet
function New-Object { param([string]`$TypeName,[object[]]`$ArgumentList)
    if(`$TypeName -eq 'System.Net.WebClient') { return [PSCustomObject]@{} | Add-Member -MemberType ScriptMethod -Name DownloadFile -Value {} -PassThru }
    & `$orig -TypeName `$TypeName -ArgumentList `$ArgumentList
}
& '$scriptPath' $escapedArgs
"@
        
        powershell.exe -NoProfile -ExecutionPolicy Bypass -Command $wrapper 2>&1 | Out-Null
        Start-Sleep -Milliseconds 100
        
        if (-not (Test-Path $script:CaptureFilePath)) {
            Write-TestResult -TestName $TestName -Passed $false -Message "Capture file not created"
            return
        }
        
        $captured = Get-Content $script:CaptureFilePath -Raw | ConvertFrom-Json
        $allMatch = $true
        $mismatches = @()
        
        foreach ($key in $ExpectedParams.Keys) {
            if ($captured.$key -ne $ExpectedParams[$key]) {
                $allMatch = $false
                $mismatches += "$key`: expected '$($ExpectedParams[$key])', got '$($captured.$key)'"
            }
        }
        
        Write-TestResult -TestName $TestName -Passed $allMatch -Message ($mismatches -join '; ')
    }
    catch {
        Write-TestResult -TestName $TestName -Passed $false -Message $_.Exception.Message
    }
}

# ========================================
# Tests
# ========================================

Write-Host "`n=== Integration Tests ===" -ForegroundColor Cyan
Initialize-MockScript

Write-Host "`n--- Basic Tests ---" -ForegroundColor Yellow

Test-Parameters -TestName "No parameters" -ArgumentString "" -ExpectedParams @{
    AddNetfx = $false; Force = $false; Version = ""
}

Test-Parameters -TestName "Single switch: -AddNetfx" -ArgumentString "-AddNetfx" -ExpectedParams @{
    AddNetfx = $true; Force = $false
}

Test-Parameters -TestName "Single switch: -Force" -ArgumentString "-Force" -ExpectedParams @{
    Force = $true; AddNetfx = $false
}

Test-Parameters -TestName "Multiple switches" -ArgumentString "-AddNetfx -Force" -ExpectedParams @{
    AddNetfx = $true; Force = $true
}

Test-Parameters -TestName "Version only" -ArgumentString "-Version 2.0.0" -ExpectedParams @{
    Version = "2.0.0"; AddNetfx = $false
}

Test-Parameters -TestName "Version with switches" -ArgumentString "-Version 2.0.0 -AddNetfx -Force" -ExpectedParams @{
    Version = "2.0.0"; AddNetfx = $true; Force = $true
}

Write-Host "`n--- String Parameter Tests ---" -ForegroundColor Yellow

Test-Parameters -TestName "RuntimeIdentifier" -ArgumentString "-RuntimeIdentifier win-x64" -ExpectedParams @{
    RuntimeIdentifier = "win-x64"
}

Test-Parameters -TestName "RuntimeIdentifier with Force" -ArgumentString "-RuntimeIdentifier osx-x64 -Force" -ExpectedParams @{
    RuntimeIdentifier = "osx-x64"; Force = $true
}

Test-Parameters -TestName "RuntimeIdentifier linux-musl" -ArgumentString "-RuntimeIdentifier linux-musl-x64" -ExpectedParams @{
    RuntimeIdentifier = "linux-musl-x64"
}

Write-Host "`n--- Colon Syntax Tests ---" -ForegroundColor Yellow
# Note: Using $true/$false as numeric :1/:0 don't work through powershell.exe -Command

Test-Parameters -TestName "Switch with colon true" -ArgumentString '-AddNetfx:$true' -ExpectedParams @{
    AddNetfx = $true
}

Test-Parameters -TestName "Switch with colon false" -ArgumentString '-AddNetfx:$false' -ExpectedParams @{
    AddNetfx = $false
}

Test-Parameters -TestName "Multiple switches with colon syntax" -ArgumentString '-AddNetfx:$true -Force:$false' -ExpectedParams @{
    AddNetfx = $true; Force = $false
}

Write-Host "`n--- Combined Tests ---" -ForegroundColor Yellow

Test-Parameters -TestName "All switches" -ArgumentString "-AddNetfx -AddNetfx48 -Force -InstallNet6 -InstallNet8 -NonSelfContained" -ExpectedParams @{
    AddNetfx=$true; AddNetfx48=$true; Force=$true; InstallNet6=$true; InstallNet8=$true; NonSelfContained=$true
}

Test-Parameters -TestName "Kitchen sink" -ArgumentString "-Version 2.0.0 -AddNetfx -Force -RuntimeIdentifier win-x64" -ExpectedParams @{
    Version="2.0.0"; AddNetfx=$true; Force=$true; RuntimeIdentifier="win-x64"
}

Write-Host "`n--- Future Parameter Tests ---" -ForegroundColor Yellow

Test-Parameters -TestName "Unknown switch param" -ArgumentString "-SomeNewSwitch" -ExpectedParams @{
    SomeNewSwitch = $true
}

Test-Parameters -TestName "Unknown value param" -ArgumentString "-SomeNewParam MyValue" -ExpectedParams @{
    SomeNewParam = "MyValue"
}

# ========================================
# Summary
# ========================================

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Passed: $script:TestsPassed" -ForegroundColor Green
Write-Host "Failed: $script:TestsFailed" -ForegroundColor $(if ($script:TestsFailed -gt 0) {'Red'} else {'Green'})

if (Test-Path $script:CaptureFilePath) { Remove-Item $script:CaptureFilePath -Force -ErrorAction SilentlyContinue }
if ($script:TestsFailed -gt 0) { exit 1 }
