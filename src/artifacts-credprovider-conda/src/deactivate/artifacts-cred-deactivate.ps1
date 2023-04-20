Write-Host "Cleanup environment variable from activation script..."
$env:NUGET_CREDENTIALPROVIDER_MSAL_ENABLED = $null
$env:ARTIFACTS_CONDA_TOKEN = $null