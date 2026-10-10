@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
dotnet run --project "%~dp0TaskAlarm.csproj" --no-launch-profile -- %*
set "TASKALARM_EXIT=%errorlevel%"
popd
if not "%TASKALARM_EXIT%"=="0" (
    echo.
    echo TaskAlarm exited with error %TASKALARM_EXIT%.
)
endlocal & exit /b %TASKALARM_EXIT%
