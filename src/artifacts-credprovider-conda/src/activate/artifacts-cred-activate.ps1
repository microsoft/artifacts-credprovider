Write-Host "Running artifacts-cred-provider-conda..."
$env:NUGET_CREDENTIALPROVIDER_MSAL_ENABLED="true"
$token = conda config --show --json | & $env:CONDA_PYTHON_EXE (Join-Path $PSScriptRoot 'artifacts-cred.py')
$env:ARTIFACTS_CONDA_TOKEN = $token
Write-Host "Set token to ARTIFACTS_CONDA_TOKEN environment variable."