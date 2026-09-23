@echo off
setlocal
cd /d "%~dp0"

set "PUBLISH_DIR=%~dp0publish\win-x64"

echo Publishing Trimble Connector (win-x64, self-contained)...
dotnet publish TrimbleConnector.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o "%PUBLISH_DIR%"
if errorlevel 1 exit /b 1

if not exist "%PUBLISH_DIR%\TrimbleConnector.exe" (
    echo [ERROR] Publish succeeded but TrimbleConnector.exe is missing in:
    echo         %PUBLISH_DIR%
    exit /b 1
)

if exist "%PUBLISH_DIR%\data" rmdir /S /Q "%PUBLISH_DIR%\data"
if exist "%PUBLISH_DIR%\appsettings.Development.json" del /F /Q "%PUBLISH_DIR%\appsettings.Development.json"
if exist "%PUBLISH_DIR%\.application_credentials" del /F /Q "%PUBLISH_DIR%\.application_credentials"
del /F /Q "%PUBLISH_DIR%\*.token" >nul 2>&1

python3 "%~dp0scripts\prepare-factory-appsettings.py" "%~dp0." "%PUBLISH_DIR%"
if errorlevel 1 (
    echo [WARN] python3 failed, trying py -3...
    py -3 "%~dp0scripts\prepare-factory-appsettings.py" "%~dp0." "%PUBLISH_DIR%"
    if errorlevel 1 (
        echo [ERROR] Could not write a clean factory appsettings.json.
        echo         Install Python 3 or keep TrimbleConnect ClientId/ClientSecret in appsettings.json.
        exit /b 1
    )
)

copy /Y "%~dp0install-service.bat" "%PUBLISH_DIR%\" >nul
if errorlevel 1 exit /b 1
copy /Y "%~dp0uninstall-service.bat" "%PUBLISH_DIR%\" >nul
if errorlevel 1 exit /b 1

echo.
echo Published a clean installer payload to publish\win-x64\TrimbleConnector.exe
echo Factory appsettings.json has no jobs, tokens, or developer folders.
echo Copied install-service.bat and uninstall-service.bat into publish\win-x64
echo.
echo Next steps:
echo   1. Compile TrimbleConnectorSetup.iss with Inno Setup to build TrimbleConnector-Setup-v2.5.5.exe
echo   2. Or run publish\win-x64\install-service.bat as Administrator
endlocal
