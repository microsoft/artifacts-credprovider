@echo off
@for /f %%i in ('conda config --show --json ^| conda run --name base --no-capture-output %CONDA_PYTHON_EXE% %~dp0artifacts-cred.py') do set ARTIFACTS_CONDA_TOKEN=%%i