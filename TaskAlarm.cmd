@echo off
dotnet run --project "%~dp0TaskAlarm.csproj" --no-launch-profile
if errorlevel 1 (
    echo.
    echo TaskAlarm could not start. Check that the .NET 10 SDK is installed.
    pause
)
