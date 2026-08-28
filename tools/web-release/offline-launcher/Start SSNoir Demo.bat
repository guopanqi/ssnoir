@echo off
setlocal
"%~dp0SSNoirDemoServer-win-x64.exe" --open
if errorlevel 1 pause
