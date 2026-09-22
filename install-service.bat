@echo off
setlocal
cd /d "%~dp0"

:: Ensure script runs with Administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please right-click install-service.bat and select 'Run as administrator'.
    pause
    exit /b 1
)

echo ===================================================
echo Installing Trimble Connector Windows Service...
echo ===================================================

set SERVICE_NAME=TrimbleConnector
set DISPLAY_NAME=Trimble Connector
set EXE_PATH=%~dp0TrimbleConnector.exe

if not exist "%EXE_PATH%" (
    echo [ERROR] TrimbleConnector.exe was not found next to this script:
    echo         %EXE_PATH%
    echo Run publish-windows.bat first, then install from publish\win-x64.
    pause
    exit /b 1
)

:: Stop and delete existing service if present
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% equ 0 (
    echo Stopping existing service...
    sc stop %SERVICE_NAME% >nul 2>&1
    timeout /t 3 >nul
    sc delete %SERVICE_NAME% >nul 2>&1
    timeout /t 2 >nul
)

:: Register Windows Service
sc create %SERVICE_NAME% binPath= "\"%EXE_PATH%\"" DisplayName= "%DISPLAY_NAME%" start= auto
if %errorLevel% neq 0 (
    echo [ERROR] sc create failed.
    pause
    exit /b 1
)

sc description %SERVICE_NAME% "Trimble Connector Daemon - Automatic Network Drive to Trimble Connect Cloud Sync Service"

:: Set failure recovery (restart service on crash)
sc failure %SERVICE_NAME% actions= restart/60000/restart/60000/restart/60000 reset= 86400

:: Start service
sc start %SERVICE_NAME%
if %errorLevel% neq 0 (
    echo [ERROR] Service was registered but did not start. Check Event Viewer.
    pause
    exit /b 1
)

echo.
echo ===================================================
echo [SUCCESS] Trimble Connector Service installed!
echo Dashboard running at: http://localhost:5000
echo ===================================================
pause
endlocal
