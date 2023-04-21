echo Running artifacts-cred-provider-conda...
set NUGET_CREDENTIALPROVIDER_MSAL_ENABLED=true
@for /f %%i in ('conda config --show --json ^| conda run --name base --no-capture-output %CONDA_PYTHON_EXE% %~dp0artifacts-cred.py') do set ARTIFACTS_CONDA_TOKEN=%%i
echo Set token to ARTIFACTS_CONDA_TOKEN environment variable.