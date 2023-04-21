echo Running artifacts-cred-provider-conda...
set NUGET_CREDENTIALPROVIDER_MSAL_ENABLED=true
for /f %%i in ('conda config --show --json ^| %CONDA_PYTHON_EXE% %~dp0%artifacts-cred.py') do "set ARTIFACTS_CONDA_TOKEN=%%i"
echo Set token to ARTIFACTS_CONDA_TOKEN environment variable.