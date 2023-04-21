$env:NUGET_CREDENTIALPROVIDER_MSAL_ENABLED="true"
$token = conda config --show --json | & $env:CONDA_PYTHON_EXE (Join-Path $PSScriptRoot 'artifacts-cred.py')
$env:ARTIFACTS_CONDA_TOKEN = $token