@echo off
setlocal
"%~dp0game\SSNoirDemoServer-win-x64.exe" --directory "%~dp0game" --open
if errorlevel 1 pause
