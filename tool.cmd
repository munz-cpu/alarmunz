@echo off
setlocal
set "HELP_EXIT=0"
if "%~1"=="" goto help
if /i "%~1"=="help" goto help
if /i "%~1"=="--help" goto help
if /i "%~1"=="-h" goto help
if /i "%~1"=="alarm" goto alarm
if /i "%~1"=="clipboard" goto clipboard
if /i "%~1"=="colorpicker" goto colorpicker
if /i "%~1"=="pocket" goto pocket
if /i "%~1"=="git" goto git

echo Unknown command: %~1
echo.
set "HELP_EXIT=1"
goto help

:alarm
if /i "%~2"=="help" goto alarm_help
if /i "%~2"=="--help" goto alarm_help
if /i "%~2"=="-h" goto alarm_help
if "%~2"=="" (
    set "HELP_EXIT=1"
    goto alarm_help
)
call "%~dp0TaskAlarm.cmd" %*
exit /b %errorlevel%

:clipboard
if /i "%~2"=="help" goto clipboard_help
if /i "%~2"=="--help" goto clipboard_help
if /i "%~2"=="-h" goto clipboard_help
if not "%~2"=="" (
    echo Unknown clipboard option: %~2
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Clipboard.ps1"
exit /b %errorlevel%

:pocket
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Pocket.ps1" %*
exit /b %errorlevel%

:colorpicker
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0ColorPicker.ps1" %2
exit /b %errorlevel%

:git
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Git.ps1" %*
exit /b %errorlevel%

:help
echo Tool commands:
echo   tool alarm       Alert when a program starts, exits, or finishes loading.
echo   tool clipboard   Show the 10 newest Windows clipboard history items.
echo   tool colorpicker Click a screen pixel and copy its HEX color.
echo   tool pocket      Store files and clipboard content temporarily.
echo   tool git push    Add, commit, and push the current Git branch.
echo   tool git pull    Reset tracked changes, then pull origin main.
echo   tool help        Show this help.
echo   tool [tool-name] help  Show details for a tool.
exit /b %HELP_EXIT%

:clipboard_help
echo Shows up to 10 newest items from Windows clipboard history ^(Win+V^).
echo Clipboard history must be enabled in Windows Settings.
exit /b 0

:alarm_help
echo Usage: tool alarm [program name] [on^|off^|loading]
echo   on       Alert when the program starts.
echo   off      Alert when the program exits.
echo   loading  Alert when Unity finishes loading ^(default^).
echo Program name is a process name without .exe.
echo Press Ctrl+C while waiting to stop the alarm.
exit /b %HELP_EXIT%
