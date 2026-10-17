param([Parameter(Mandatory = $true)][string]$Name)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Name) -or $Name.Contains("`n") -or $Name.Contains("`r")) {
    [Console]::Error.WriteLine('Provide one non-empty gitignore pattern.')
    exit 1
}

$root = try { & git rev-parse --show-toplevel 2>$null } catch { $null }
if ($LASTEXITCODE -ne 0 -or -not $root) {
    [Console]::Error.WriteLine('Current directory is not inside a Git repository.')
    exit 1
}

$ignorePath = Join-Path $root '.gitignore'
$content = if (Test-Path -LiteralPath $ignorePath) {
    [System.IO.File]::ReadAllText($ignorePath)
} else { '' }
$lines = $content -split "`r?`n"
if ($lines -ccontains $Name) {
    Write-Host "Already ignored: $Name"
    exit 0
}

$newline = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
if ($content.Length -gt 0 -and -not $content.EndsWith("`n")) { $content += $newline }
$content += $Name + $newline
[System.IO.File]::WriteAllText($ignorePath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Host "Added to ${ignorePath}: $Name"
