@echo off
if /i "%~1"=="alarm" (
    call "%~dp0TaskAlarm.cmd"
    exit /b %errorlevel%
)

echo Usage: tool alarm
if not "%~1"=="" echo Unknown tool: %~1
exit /b 1
