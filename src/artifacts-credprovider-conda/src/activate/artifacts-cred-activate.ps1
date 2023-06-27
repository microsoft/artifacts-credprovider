$token = conda config --show --json | & $Env:CONDA_EXE run --name base --no-capture-output python (Join-Path $PSScriptRoot 'artifacts-cred.py')
$env:ARTIFACTS_CONDA_TOKEN = $token