$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'ToolInertia\ToolInertia.csproj'
$exe = Join-Path $PSScriptRoot 'ToolInertia\bin\Release\net10.0-windows\ToolInertia.exe'

try {
    $running = Get-Process ToolInertia -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe } |
        Select-Object -First 1
    if ($running) {
        Write-Output 'slipperyWindow is already running. Open settings from the system tray.'
        exit 0
    }

    & dotnet build $project -c Release --nologo -v:q -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
    Write-Output 'slipperyWindow started. Open settings from the system tray.'
    exit 0
} catch {
    [Console]::Error.WriteLine('slipperyWindow start failed: ' + $_.Exception.Message)
    exit 1
}
