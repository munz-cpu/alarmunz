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
if /i "%~1"=="git" goto git
if /i "%~1"=="gui" goto gui
if /i "%~1"=="dice" goto dice

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

:colorpicker
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0ColorPicker.ps1" %2
exit /b %errorlevel%

:git
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Git.ps1" %*
exit /b %errorlevel%

:gui
if /i "%~2"=="help" goto gui_help
if /i "%~2"=="--help" goto gui_help
if /i "%~2"=="-h" goto gui_help
if not "%~2"=="" (
    echo Usage: tool gui
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0ToolGui.ps1"
exit /b %errorlevel%

:dice
if /i "%~2"=="help" goto dice_help
if /i "%~2"=="--help" goto dice_help
if /i "%~2"=="-h" goto dice_help
if not "%~2"=="" (
    echo Usage: tool dice
    exit /b 1
)
powershell.exe -NoProfile -Command "Get-Random -Minimum 1 -Maximum 7"
exit /b %errorlevel%

:help
echo Tool commands:
echo   tool alarm       Alert when a program starts, exits, or finishes loading.
echo   tool clipboard   Show the 10 newest Windows clipboard history items.
echo   tool colorpicker Click a screen pixel and copy its HEX color.
echo   tool git push [message] [branch]  Commit and push ^(defaults: dummy, main^).
echo   tool git pull    Reset tracked changes, then pull origin main.
echo   tool gui         Open the Tool GUI utility hub.
echo   tool dice        Roll a die and print a number from 1 to 6.
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

:dice_help
echo Usage: tool dice
echo Prints one random number from 1 to 6.
exit /b 0

:gui_help
echo Usage: tool gui
echo Opens the Tool utility hub. Create a window PIP or select part of a window.
exit /b 0
