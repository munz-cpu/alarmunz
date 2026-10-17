$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'ToolInertia\ToolInertia.csproj'
$exe = Join-Path $PSScriptRoot 'ToolInertia\bin\Release\net10.0-windows\ToolInertia.exe'

try {
    $running = @(Get-Process ToolInertia -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe })
    if ($running.Count -gt 0) {
        $running | Stop-Process -ErrorAction Stop
        Write-Output 'slipperyWindow stopped.'
        exit 0
    }

    & dotnet build $project -c Release --nologo -v:q -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
    Write-Output 'slipperyWindow started. Run tool slipperyWindow again to stop.'
    exit 0
} catch {
    [Console]::Error.WriteLine('slipperyWindow failed: ' + $_.Exception.Message)
    exit 1
}
