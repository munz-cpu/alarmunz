$ErrorActionPreference = 'Stop'

$store = if ($env:TOOL_POCKET_HOME) { $env:TOOL_POCKET_HOME } else { Join-Path $env:LOCALAPPDATA 'ToolPocket' }
$itemsDir = Join-Path $store 'items'

function Show-Help {
    Write-Host 'Tool Pocket - temporary storage for files and clipboard content'
    Write-Host '  tool pocket                 Show commands'
    Write-Host '  tool pocket list            List stored items'
    Write-Host '  tool pocket add <path...>   Store copies of files or folders'
    Write-Host '  tool pocket clip            Store current clipboard text, image, or files'
    Write-Host '  tool pocket text            Paste text; finish with .done on its own line'
    Write-Host '  tool pocket text "words"    Store text given on the command line'
    Write-Host '  tool pocket show <number>   Print stored text'
    Write-Host '  tool pocket copy <number> [folder]  Copy file to folder, or content to clipboard'
    Write-Host '  tool pocket take <number> [folder]  Copy out and remove from Pocket'
    Write-Host '  tool pocket remove <number> Remove an item'
    Write-Host '  tool pocket help            Show this help'
    Write-Host ('Storage: {0}' -f $store)
}

function Get-Items {
    if (-not (Test-Path -LiteralPath $itemsDir)) { return @() }
    return @(Get-ChildItem -LiteralPath $itemsDir -Directory | ForEach-Object {
        $metaPath = Join-Path $_.FullName 'meta.json'
        if (Test-Path -LiteralPath $metaPath) {
            $meta = Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
            [pscustomobject]@{ Meta = $meta; Directory = $_.FullName }
        }
    } | Sort-Object { $_.Meta.Created } -Descending)
}

function Get-ItemByNumber($number) {
    $items = @(Get-Items)
    $index = 0
    if (-not [int]::TryParse($number, [ref]$index) -or $index -lt 1 -or $index -gt $items.Count) {
        throw '유효한 항목 번호를 입력하세요. tool pocket list로 목록을 확인할 수 있습니다.'
    }
    return $items[$index - 1]
}

function New-PocketItem($kind, $name, $source) {
    [void][System.IO.Directory]::CreateDirectory($itemsDir)
    $id = '{0}-{1}' -f (Get-Date -Format 'yyyyMMddHHmmssfff'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
    $directory = Join-Path $itemsDir $id
    [void][System.IO.Directory]::CreateDirectory($directory)
    $meta = [pscustomobject]@{ Id = $id; Kind = $kind; Name = $name; Source = $source; Created = (Get-Date).ToString('o') }
    return [pscustomobject]@{ Meta = $meta; Directory = $directory }
}

function Save-Meta($item) {
    $item.Meta | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $item.Directory 'meta.json') -Encoding UTF8
}

function Add-Path($path) {
    $resolved = (Resolve-Path -LiteralPath $path -ErrorAction Stop).Path
    $source = Get-Item -LiteralPath $resolved -Force
    $kind = if ($source.PSIsContainer) { 'folder' } else { 'file' }
    $item = New-PocketItem $kind $source.Name $resolved
    try {
        Copy-Item -LiteralPath $resolved -Destination (Join-Path $item.Directory 'payload') -Recurse -ErrorAction Stop
        Save-Meta $item
        Write-Host ('맡아 둠: {0}' -f $source.Name)
    } catch {
        Remove-Item -LiteralPath $item.Directory -Recurse -Force
        throw
    }
}

function Add-Clipboard {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $clipboard = [System.Windows.Forms.Clipboard]
    if ($clipboard::ContainsFileDropList()) {
        foreach ($path in $clipboard::GetFileDropList()) { Add-Path $path }
        return
    }
    if ($clipboard::ContainsImage()) {
        $item = New-PocketItem 'image' 'clipboard.png' ''
        try {
            $image = $clipboard::GetImage()
            try { $image.Save((Join-Path $item.Directory 'payload.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
            finally { $image.Dispose() }
            Save-Meta $item
            Write-Host '맡아 둠: 클립보드 이미지'
        } catch {
            Remove-Item -LiteralPath $item.Directory -Recurse -Force
            throw
        }
        return
    }
    if ($clipboard::ContainsText()) {
        Add-Text $clipboard::GetText() 'clipboard text'
        return
    }
    throw '클립보드에 보관할 수 있는 텍스트, 이미지 또는 파일이 없습니다.'
}

function Add-Text($text, $name) {
    if ([string]::IsNullOrEmpty($text)) { throw '빈 글은 보관할 수 없습니다.' }
    $item = New-PocketItem 'text' $name ''
    try {
        [System.IO.File]::WriteAllText((Join-Path $item.Directory 'payload.txt'), $text, [System.Text.UTF8Encoding]::new($false))
        Save-Meta $item
        Write-Host ('맡아 둠: {0}' -f $name)
    } catch {
        Remove-Item -LiteralPath $item.Directory -Recurse -Force
        throw
    }
}

function Export-Item($item, $destination) {
    $kind = $item.Meta.Kind
    if ($kind -eq 'text') {
        Add-Type -AssemblyName System.Windows.Forms
        $text = [System.IO.File]::ReadAllText((Join-Path $item.Directory 'payload.txt'))
        [System.Windows.Forms.Clipboard]::SetText($text)
        Write-Host '텍스트를 클립보드에 복사했습니다.'
        return
    }
    if ($kind -eq 'image') {
        Add-Type -AssemblyName System.Windows.Forms
        Add-Type -AssemblyName System.Drawing
        $image = [System.Drawing.Image]::FromFile((Join-Path $item.Directory 'payload.png'))
        try { [System.Windows.Forms.Clipboard]::SetImage($image) }
        finally { $image.Dispose() }
        Write-Host '이미지를 클립보드에 복사했습니다.'
        return
    }
    if (-not $destination) { $destination = (Get-Location).Path }
    $folder = (Resolve-Path -LiteralPath $destination -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw '대상은 기존 폴더여야 합니다.' }
    $target = Join-Path $folder $item.Meta.Name
    if (Test-Path -LiteralPath $target) { throw ('대상에 같은 이름이 이미 있습니다: {0}' -f $target) }
    Copy-Item -LiteralPath (Join-Path $item.Directory 'payload') -Destination $target -Recurse -ErrorAction Stop
    Write-Host ('꺼냄: {0}' -f $target)
}

try {
    $arguments = @($args)
    if ($arguments.Count -gt 0 -and $arguments[0] -eq 'pocket') { $arguments = @($arguments | Select-Object -Skip 1) }
    $command = if ($arguments.Count) { $arguments[0].ToLowerInvariant() } else { 'help' }
    switch ($command) {
        { $_ -in @('help', '--help', '-h') } { Show-Help; break }
        'list' {
            $items = @(Get-Items)
            if ($items.Count -eq 0) { Write-Host 'Pocket이 비어 있습니다. tool pocket add <파일> 또는 tool pocket clip'; break }
            for ($i = 0; $i -lt $items.Count; $i++) {
                Write-Host ('{0,2}. [{1}] {2}  {3}' -f ($i + 1), $items[$i].Meta.Kind, $items[$i].Meta.Name, $items[$i].Meta.Created)
            }
            break
        }
        'add' {
            if ($arguments.Count -lt 2) { throw '사용법: tool pocket add <파일 또는 폴더 경로...>' }
            foreach ($path in $arguments[1..($arguments.Count - 1)]) { Add-Path $path }
            break
        }
        'clip' { Add-Clipboard; break }
        'text' {
            if ($arguments.Count -gt 1) {
                Add-Text ($arguments[1..($arguments.Count - 1)] -join ' ') 'pasted text'
                break
            }
            Write-Host '글을 붙여넣으세요. 끝나면 새 줄에 .done을 입력하세요.'
            $lines = [System.Collections.Generic.List[string]]::new()
            while ($null -ne ($line = [Console]::ReadLine())) {
                if ($line -eq '.done') { break }
                $lines.Add($line)
            }
            Add-Text ($lines -join [Environment]::NewLine) 'pasted text'
            break
        }
        'show' {
            if ($arguments.Count -ne 2) { throw '사용법: tool pocket show <번호>' }
            $item = Get-ItemByNumber $arguments[1]
            if ($item.Meta.Kind -ne 'text') { throw '텍스트 항목만 표시할 수 있습니다.' }
            Write-Host ([System.IO.File]::ReadAllText((Join-Path $item.Directory 'payload.txt')))
            break
        }
        { $_ -in @('copy', 'take') } {
            if ($arguments.Count -lt 2 -or $arguments.Count -gt 3) { throw '사용법: tool pocket copy|take <번호> [대상 폴더]' }
            $item = Get-ItemByNumber $arguments[1]
            $destination = if ($arguments.Count -eq 3) { $arguments[2] } else { $null }
            Export-Item $item $destination
            if ($command -eq 'take') { Remove-Item -LiteralPath $item.Directory -Recurse -Force }
            break
        }
        'remove' {
            if ($arguments.Count -ne 2) { throw '사용법: tool pocket remove <번호>' }
            $item = Get-ItemByNumber $arguments[1]
            Remove-Item -LiteralPath $item.Directory -Recurse -Force
            Write-Host ('삭제함: {0}' -f $item.Meta.Name)
            break
        }
        default { throw ('알 수 없는 Pocket 명령: {0}. tool pocket help를 확인하세요.' -f $command) }
    }
} catch {
    [Console]::Error.WriteLine('Pocket 오류: ' + $_.Exception.Message)
    exit 1
}
