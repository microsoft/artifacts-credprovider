@echo OFF
SETLOCAL EnableDelayedExpansion

@REM A Windows domain user should be able to run this against a feed in an AAD-back AzDO org
@REM and all scenarios should succeed non-interactively.

set TEST_FEED=%1
set NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_ENABLED=true
set NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION=%TEMP%\msal.cache
IF EXIST %NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION% (del /q %NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION%)

echo "Testing MSAL with broker"
set NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=true
CALL :TEST_FRAMEWORKS
IF %ERRORLEVEL% NEQ 0 (
    echo "Failed: %ERRORLEVEL%"
    exit /b %ERRORLEVEL%
)

echo "Testing MSAL without broker"
set NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=false
CALL :TEST_FRAMEWORKS
IF %ERRORLEVEL% NEQ 0 (
    echo "Failed: %ERRORLEVEL%"
    exit /b %ERRORLEVEL%
)

echo "All tests passed!"
exit /b 0


:TEST_FRAMEWORKS
for %%I in ("netcoreapp3.1","net461","net6.0") DO (
    del /q "!UserProfile!\AppData\Local\MicrosoftCredentialProvider\*.dat" 2>NUL
    del /q "%NUGET_CREDENTIALPROVIDER_MSAL_FILECACHE_LOCATION%" 2>NUL
    echo Testing %%I with NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER=!NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER!
    echo dotnet run --no-restore --no-build -f %%I --project CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj -- -C -U !TEST_FEED! -V Debug
    dotnet run --no-restore --no-build -f %%I --project CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj -- -C -U !TEST_FEED! -V Debug ^
        > test.%%I.%NUGET_CREDENTIALPROVIDER_MSAL_ALLOW_BROKER%.log
    IF !ERRORLEVEL! NEQ 0 (
        echo "Previous command execution failed: !ERRORLEVEL!"
        dotnet run --no-restore --no-build -f %%I --project CredentialProvider.Microsoft\CredentialProvider.Microsoft.csproj -- -C -U !TEST_FEED! -V Debug
        exit /b !ERRORLEVEL!
    )
)
