param([switch]$Run, [switch]$Help)

$stateDir = Join-Path $env:LOCALAPPDATA 'ganzi-terminal'
$statePath = Join-Path $stateDir 'state.json'
$stopPath = Join-Path $stateDir 'stop'

if ($Help) {
    Write-Host 'Usage: ganzi  (run again to close the display)'
    Write-Host 'Also available as: tool ganzi'
    exit 0
}

function Get-ActiveWorker {
    if (-not (Test-Path -LiteralPath $statePath)) { return $null }
    try {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        $process = Get-Process -Id ([int]$state.Pid) -ErrorAction Stop
        if ($process.StartTime.ToUniversalTime().Ticks -ne [long]$state.StartTicks) { return $null }
        return $process
    } catch { return $null }
}

if ($Run) {
    New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
    $me = Get-Process -Id $PID
    @{ Pid = $PID; StartTicks = $me.StartTime.ToUniversalTime().Ticks } |
        ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
    $esc = [char]27
    $colors = @("$esc[97m", "$esc[37m", "$esc[38;5;46m", "$esc[38;5;214m")
    $reset = "$esc[0m"
    function New-Hex([int]$length) {
        $alphabet = '0123456789ABCDEF'
        -join (1..$length | ForEach-Object { $alphabet[(Get-Random -Minimum 0 -Maximum 16)] })
    }
    function Show-Line([string]$message, [int]$colorIndex = -1) {
        if ($colorIndex -lt 0) { $colorIndex = @(0, 0, 0, 1, 1, 2)[(Get-Random -Minimum 0 -Maximum 6)] }
        $stamp = Get-Date -Format 'HH:mm:ss.fff'
        Write-Host "$($colors[$colorIndex])[$stamp]  $message$reset"
    }
    $paths = @('/dev/tty7', '/sys/mesh/relay', '/var/cache/ghost', '/proc/4096/maps', '/opt/ganzi/core')
    $verbs = @('handshake', 'route.resolve', 'cipher.rotate', 'trace.inject', 'frame.decode', 'entropy.mix', 'cache.flush', 'node.relay')
    $folders = @('core', 'engine', 'modules', 'runtime', 'protocol', 'cache', 'signals')
    $files = @('cipher', 'relay', 'session', 'buffer', 'trace', 'index', 'gateway', 'kernel')
    function Show-Bulk {
        Write-Host "${esc}[37m  $ git add .  // staging virtual files${reset}"
        $count = Get-Random -Minimum 14 -Maximum 34
        for ($j = 0; $j -lt $count -and -not (Test-Path -LiteralPath $stopPath); $j++) {
            $folder = $folders[(Get-Random -Minimum 0 -Maximum $folders.Count)]
            $file = $files[(Get-Random -Minimum 0 -Maximum $files.Count)]
            $suffix = Get-Random -Minimum 10 -Maximum 999
            $status = @('add', 'hash', 'stage', 'index')[(Get-Random -Minimum 0 -Maximum 4)]
            Write-Host "${esc}[37m  [$status]  src/$folder/$file.$suffix  $(New-Hex 8)${reset}"
        }
        Write-Host "${esc}[38;5;46m  [OK] $count objects staged  |  tree=$(New-Hex 12)${reset}"
    }
    function Show-Loading {
        $task = @('compiling modules', 'resolving dependencies', 'decrypting payload', 'building index', 'synchronizing nodes')[(Get-Random -Minimum 0 -Maximum 5)]
        $frames = @('|', '/', '-', '\')
        $progress = 0
        $tick = 0
        while ($progress -lt 100 -and -not (Test-Path -LiteralPath $stopPath)) {
            $tick++
            if ($tick % 2 -eq 0) {
                $progress = [math]::Min(100, $progress + (Get-Random -Minimum 2 -Maximum 10))
            }
            $filled = [math]::Floor($progress / 4)
            $bar = ('#' * $filled).PadRight(25, '-')
            $spinner = $frames[$tick % $frames.Count]
            [Console]::Write("`r${esc}[2K${esc}[97m  $spinner  $task  [$bar] $progress%${reset}")
            Start-Sleep -Milliseconds (Get-Random -Minimum 65 -Maximum 165)
        }
        if (Test-Path -LiteralPath $stopPath) {
            [Console]::Write("`r${esc}[2K")
            return
        }
        [Console]::WriteLine("`r${esc}[2K${esc}[38;5;46m  [OK] $task  [#########################] 100%${reset}")
    }
    try {
        Clear-Host
        Write-Host "${esc}[97m  G  A  N  Z  I  / /  T E R M I N A L${reset}"
        Write-Host "${esc}[37m  ======================================${reset}"
        Write-Host ''
        Write-Host "${esc}[38;5;214m  VISUAL SIMULATION  //  no real systems accessed${reset}"
        Write-Host "  Run ganzi again to stop. Ctrl+C also works."
        Write-Host ''
        $nextLoadingAt = (Get-Date).AddSeconds((Get-Random -Minimum 40 -Maximum 71))
        while (-not (Test-Path -LiteralPath $stopPath)) {
            $burst = Get-Random -Minimum 3 -Maximum 16
            for ($i = 0; $i -lt $burst -and -not (Test-Path -LiteralPath $stopPath); $i++) {
                $a = Get-Random -Minimum 10 -Maximum 9999
                $b = Get-Random -Minimum 1 -Maximum 999
                $c = Get-Random -Minimum 1 -Maximum 99
                switch (Get-Random -Minimum 0 -Maximum 12) {
                    0 { Show-Line "[root@ganzi]$ sudo ./specter --phase=$a --silent" 0 }
                    1 { Show-Line "$(New-Hex 8)  $(New-Hex 32)  |..$(New-Hex 4)..|" 0 }
                    2 { Show-Line "$($verbs[(Get-Random -Minimum 0 -Maximum $verbs.Count)])  session=$(New-Hex 12)  status=OK" }
                    3 { Show-Line "tcp  198.51.100.${c}:$b -> 203.0.113.${c}:443  seq=$a  ack=$b" 1 }
                    4 { Show-Line "sha256:$(New-Hex 32)  VERIFIED" 0 }
                    5 { Show-Line "memory.map  0x$(New-Hex 8)-0x$(New-Hex 8)  rwx  $(New-Hex 6)" }
                    6 { Show-Line "tunnel[$(New-Hex 4)]  relay=$b  jitter=${c}ms  encrypted=true" 0 }
                    7 { Show-Line "$($paths[(Get-Random -Minimum 0 -Maximum $paths.Count)])  blocks=$a  sync=$c%" }
                    8 { Show-Line "[WARN] anomaly at sector 0x$(New-Hex 6)  rerouting..." 3 }
                    9 { Show-Line "[OK]   anomaly contained  signature=$(New-Hex 8)" 0 }
                    10 { Show-Line "scan.queue     shard=$a  pending=$b  state=READY" 1 }
                    11 { Show-Line "daemon[$a]  fork=$b  fd=$c  heartbeat=stable" }
                }
                if ((Get-Random -Minimum 0 -Maximum 8) -eq 0) {
                    Start-Sleep -Milliseconds (Get-Random -Minimum 220 -Maximum 650)
                } else {
                    Start-Sleep -Milliseconds (Get-Random -Minimum 20 -Maximum 105)
                }
            }
            if ((Get-Random -Minimum 0 -Maximum 3) -eq 0 -and -not (Test-Path -LiteralPath $stopPath)) {
                Show-Bulk
            }
            if ((Get-Date) -ge $nextLoadingAt -and -not (Test-Path -LiteralPath $stopPath)) {
                Show-Loading
                $nextLoadingAt = (Get-Date).AddSeconds((Get-Random -Minimum 40 -Maximum 71))
            }
            if ((Get-Random -Minimum 0 -Maximum 4) -eq 0) {
                Write-Host "${esc}[37m  >>> PHASE $(New-Hex 4) / SIGNAL LOCKED / $(Get-Date -Format 'HH:mm:ss') <<<${reset}"
            }
            Start-Sleep -Milliseconds (Get-Random -Minimum 180 -Maximum 1100)
        }
    } finally {
        $active = Get-ActiveWorker
        if ($active -and $active.Id -eq $PID) {
            Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $stopPath -Force -ErrorAction SilentlyContinue
        }
    }
    exit 0
}

New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
$mutex = [System.Threading.Mutex]::new($false, 'Local\GanziTerminalToggle')
try {
    if (-not $mutex.WaitOne(5000)) { throw 'Another ganzi command is starting. Try again.' }
    $worker = Get-ActiveWorker
    if ($worker) {
        New-Item -ItemType File -Path $stopPath -Force | Out-Null
        Write-Host 'Ganzi display stopping.'
    } else {
        Remove-Item -LiteralPath $statePath, $stopPath -Force -ErrorAction SilentlyContinue
        $script = $MyInvocation.MyCommand.Path
        $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -Run' -f $script
        Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments | Out-Null
        for ($attempt = 0; $attempt -lt 30 -and -not (Get-ActiveWorker); $attempt++) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Get-ActiveWorker)) { throw 'The display did not start.' }
        Write-Host 'Ganzi display started. Run ganzi again to stop.'
    }
} finally {
    try { $mutex.ReleaseMutex() } catch { }
    $mutex.Dispose()
}
