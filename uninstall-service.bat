@echo off
setlocal

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please right-click uninstall-service.bat and select 'Run as administrator'.
    pause
    exit /b 1
)

echo ===================================================
echo Uninstalling Trimble Connector Windows Service...
echo ===================================================

set SERVICE_NAME=TrimbleConnector

sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% neq 0 (
    echo Service %SERVICE_NAME% is not installed.
    echo.
    echo ===================================================
    echo [SUCCESS] Trimble Connector Service removed.
    echo ===================================================
    pause
    exit /b 0
)

sc stop %SERVICE_NAME% >nul 2>&1
timeout /t 3 >nul
sc delete %SERVICE_NAME% >nul 2>&1

echo.
echo ===================================================
echo [SUCCESS] Trimble Connector Service removed.
echo ===================================================
pause
endlocal
