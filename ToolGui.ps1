$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'ToolSlice\ToolSlice.csproj'
$exe = Join-Path $PSScriptRoot 'ToolSlice\bin\Gui\net10.0-windows10.0.19041.0\ToolSlice.exe'

try {
    $running = Get-Process ToolSlice -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe -and $_.MainWindowHandle -ne [IntPtr]::Zero } |
        Select-Object -First 1
    if ($running) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ToolGuiWindow {
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@
        [void][ToolGuiWindow]::ShowWindowAsync($running.MainWindowHandle, 9)
        [void][ToolGuiWindow]::SetForegroundWindow($running.MainWindowHandle)
        exit 0
    }

    & dotnet build $project -c Gui --nologo -v:q -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Start-Process -FilePath $exe -WorkingDirectory (Get-Location).Path
    exit 0
} catch {
    [Console]::Error.WriteLine('Tool GUI 실행 실패: ' + $_.Exception.Message)
    exit 1
}
