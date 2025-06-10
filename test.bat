@echo OFF
SETLOCAL EnableDelayedExpansion

@REM A Windows domain user should be able to run this against a feed in an AAD-back AzDO org
@REM and all scenarios should succeed non-interactively.

IF "%~1" == "" (
    echo "Please specify an AzDO organization package feed URL as the first parameter."
    exit /b 1
)

set TEST_FEED=%1

REM --- Test with legacy NUGET_CREDENTIALPROVIDER_* variables ---
echo "==== Testing with legacy NUGET_CREDENTIALPROVIDER_* environment variables ===="
set NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED=true
set NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION=%TEMP%\msal.cache
IF EXIST %NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION% (del /q %NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION%)

set NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=true
CALL :TEST_FRAMEWORKS legacy true
IF %ERRORLEVEL% NEQ 0 (
    echo "Failed: %ERRORLEVEL% (legacy, broker=true)"
    exit /b %ERRORLEVEL%
)

set NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=false
CALL :TEST_FRAMEWORKS legacy false
IF %ERRORLEVEL% NEQ 0 (
    echo "Failed: %ERRORLEVEL% (legacy, broker=false)"
    exit /b %ERRORLEVEL%
)

REM --- Unset legacy variables before testing new variables ---
set NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED=
set NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION=
set NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=

REM --- Test with new ARTIFACTS_CREDENTIALPROVIDER_* variables ---
echo "==== Testing with new ARTIFACTS_CREDENTIALPROVIDER_* environment variables ===="
set ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED=true
set ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION=%TEMP%\msal.cache
IF EXIST %ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION% (del /q %ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION%)

set ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=true
CALL :TEST_FRAMEWORKS new true
IF %ERRORLEVEL% NEQ 0 (
    echo "Failed: %ERRORLEVEL% (new, broker=true)"
    exit /b %ERRORLEVEL%
)

set ARTIFACTS_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=false
CALL :TEST_FRAMEWORKS new false
IF %ERRORLEVEL% NEQ 0 (
    echo "Failed: %ERRORLEVEL% (new, broker=false)"
    exit /b %ERRORLEVEL%
)

echo "All tests passed!"
exit /b 0

:TEST_FRAMEWORKS
REM %1 = envtype (legacy/new), %2 = broker (true/false)
for %%I in ("net481","net6.0","net8.0") DO (
    del /q "!UserProfile!\AppData\Local\MicrosoftCredentialProvider\*.dat" 2>NUL
    if "%1"=="legacy" del /q "%NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION%" 2>NUL
    if "%1"=="new" del /q "%ARTIFACTS_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION%" 2>NUL
    echo Testing %%I with %1 envvars, broker=%2
    echo dotnet run --no-restore --no-build -f %%I --project CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj -- -C -U !TEST_FEED! -V Debug
    dotnet run --no-restore --no-build -f %%I --project CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj -- -C -U !TEST_FEED! -V Debug ^
        > test.%%I.%1.%2.log
    IF !ERRORLEVEL! NEQ 0 (
        echo "Previous command execution failed: !ERRORLEVEL!"
        dotnet run --no-restore --no-build -f %%I --project CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj -- -C -U !TEST_FEED! -V Debug
        exit /b !ERRORLEVEL!
    )
)
