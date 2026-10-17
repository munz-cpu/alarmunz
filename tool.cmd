@echo off
setlocal
set "HELP_EXIT=0"
if "%~1"=="" goto help
if /i "%~1"=="help" goto help
if /i "%~1"=="h" goto help
if /i "%~1"=="--help" goto help
if /i "%~1"=="-h" goto help
if /i "%~1"=="alarm" goto alarm
if /i "%~1"=="a" goto alarm
if /i "%~1"=="clipboard" goto clipboard
if /i "%~1"=="cb" goto clipboard
if /i "%~1"=="colorpicker" goto colorpicker
if /i "%~1"=="cp" goto colorpicker
if /i "%~1"=="git" goto git
if /i "%~1"=="gitignore" goto gitignore
if /i "%~1"=="gui" goto gui
if /i "%~1"=="dice" goto dice
if /i "%~1"=="decibel" goto decibel
if /i "%~1"=="slipperyWindow" goto slipperyWindow
if /i "%~1"=="sw" goto slipperyWindow
if /i "%~1"=="ganzi" goto ganzi

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

:gitignore
if /i "%~2"=="help" goto gitignore_help
if /i "%~2"=="--help" goto gitignore_help
if "%~2"=="" goto gitignore_error
if not "%~3"=="" goto gitignore_error
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Gitignore.ps1" %2
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

:decibel
if /i "%~2"=="help" goto decibel_help
if /i "%~2"=="--help" goto decibel_help
if /i "%~2"=="-h" goto decibel_help
if /i "%~2"=="calibrate" (
    if "%~3"=="" goto decibel_help_error
    if not "%~4"=="" goto decibel_help_error
    call "%~dp0TaskAlarm.cmd" decibel calibrate "%~3"
    exit /b %errorlevel%
)
if /i "%~2"=="--raw" (
    if not "%~3"=="" goto decibel_help_error
    call "%~dp0TaskAlarm.cmd" decibel --raw
    exit /b %errorlevel%
)
if not "%~2"=="" goto decibel_help_error
call "%~dp0TaskAlarm.cmd" decibel
exit /b %errorlevel%

:slipperyWindow
if /i "%~2"=="help" goto slipperyWindow_help
if /i "%~2"=="--help" goto slipperyWindow_help
if /i "%~2"=="-h" goto slipperyWindow_help
if not "%~2"=="" (
    echo Usage: tool slipperyWindow
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SlipperyWindow.ps1"
exit /b %errorlevel%

:ganzi
call "%~dp0ganzi.cmd" %2
exit /b %errorlevel%

:help
echo Tool commands:
echo   tool alarm ^(a^)        Alert when a program starts, exits, or finishes loading.
echo   tool clipboard ^(cb^)  Show the 10 newest Windows clipboard history items.
echo   tool colorpicker ^(cp^) Click a screen pixel and copy its HEX color.
echo   tool git push [message] [branch]  Commit and push ^(defaults: dummy, main^).
echo   tool git pull    Reset tracked changes, then pull origin main.
echo   tool gitignore [name]  Add a pattern to this repo's .gitignore.
echo   tool gui         Open the Tool GUI utility hub.
echo   tool dice        Roll a die and print a number from 1 to 6.
echo   tool decibel     Show calibrated microphone sound level in dB SPL.
echo   tool slipperyWindow ^(sw^) Toggle window inertia in the system tray.
echo   tool ganzi       Toggle the animated terminal display.
echo   tool help ^(h^)    Show this help.
echo   tool [tool-name] help  Show details for a tool.
exit /b %HELP_EXIT%

:clipboard_help
echo Shows up to 10 newest items from Windows clipboard history ^(Win+V^).
echo Clipboard history must be enabled in Windows Settings.
exit /b 0

:alarm_help
echo Usage: tool alarm ^(a^) [program name] [on^|off^|loading]
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

:decibel_help
echo Usage: tool decibel
echo        tool decibel calibrate [reference dB SPL]
echo        tool decibel --raw
echo Calibrate against a sound level meter held beside the default microphone.
echo Keep the reference sound steady for 3 seconds. Press Ctrl+C to stop monitoring.
echo Changing the microphone or its gain requires recalibration.
exit /b 0

:decibel_help_error
set "HELP_EXIT=1"
goto decibel_help

:gui_help
echo Usage: tool gui
echo Opens the Tool utility hub. Create a window PIP or select part of a window.
exit /b 0

:gitignore_help
echo Usage: tool gitignore [name]
echo Adds the pattern to the current Git repository's root .gitignore.
exit /b 0

:gitignore_error
echo Usage: tool gitignore [name]
exit /b 1

:slipperyWindow_help
echo Usage: tool slipperyWindow ^(sw^)
echo Starts the window inertia utility, or stops it if already running.
echo Open its settings from the system tray while running.
exit /b 0
