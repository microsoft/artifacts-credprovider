export NUGET_CREDENTIALPROVIDER_MSAL_ENABLED="true"
export ARTIFACTS_CONDA_TOKEN=$(eval conda config --show --json | eval $CONDA_PYTHON_EXE $BASH_SOURCE/../artifacts-cred.py)