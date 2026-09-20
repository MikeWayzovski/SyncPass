@echo off
setlocal
cd /d "%~dp0"

dotnet publish TrimbleConnector.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o "%~dp0publish\win-x64"
if errorlevel 1 exit /b 1

echo Published Trimble Connector to publish\win-x64\TrimbleConnector.exe
endlocal
