@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Ganzi.ps1" %*
exit /b %errorlevel%
