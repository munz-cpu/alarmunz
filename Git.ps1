$ErrorActionPreference = 'Stop'

function Show-Help {
    Write-Host 'Usage: tool git push <commit message> <branch name>'
    Write-Host '       tool git pull [remote branch]'
    Write-Host 'Push runs git add ., git commit -m <message>, then git push origin <branch>.'
    Write-Host 'Pull without arguments runs git reset --hard HEAD, then git pull origin main.'
    Write-Host 'For a commit message with spaces, quote it or put the branch name last.'
}

try {
    $arguments = @($args)
    if ($arguments.Count -gt 0 -and $arguments[0] -eq 'git') { $arguments = @($arguments | Select-Object -Skip 1) }
    if ($arguments.Count -eq 0 -or ($arguments.Count -eq 1 -and $arguments[0] -in @('help', '--help', '-h'))) {
        Show-Help
        exit 0
    }
    $command = $arguments[0]
    $arguments = @($arguments | Select-Object -Skip 1)
    if ($command -notin @('push', 'pull')) {
        Show-Help
        exit 1
    }

    $inside = & git rev-parse --is-inside-work-tree 2>$null
    if ($LASTEXITCODE -ne 0 -or $inside -ne 'true') { throw '현재 폴더가 Git 저장소 안에 있지 않습니다.' }

    if ($command -eq 'pull') {
        if ($arguments.Count -eq 0) {
            Write-Host 'git reset --hard HEAD'
            & git reset --hard HEAD
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

            Write-Host 'git pull origin main'
            & git pull origin main
        } else {
            Write-Host ('git pull {0}' -f ($arguments -join ' '))
            & git pull @arguments
        }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        exit 0
    }

    if ($arguments.Count -lt 2) {
        Show-Help
        exit 1
    }

    $branch = $arguments[-1]
    $message = ($arguments[0..($arguments.Count - 2)] -join ' ').Trim()
    if (-not $message) { throw '커밋 메시지를 입력하세요.' }

    & git check-ref-format --branch $branch >$null 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('유효하지 않은 브랜치 이름: {0}' -f $branch) }

    $current = & git branch --show-current
    if ($LASTEXITCODE -ne 0 -or -not $current) { throw '현재 체크아웃된 브랜치를 확인할 수 없습니다.' }
    if ($current -ne $branch) { throw ('현재 브랜치는 {0}입니다. 지정한 {1} 브랜치로 먼저 전환하세요.' -f $current, $branch) }

    & git remote get-url origin >$null 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'origin 원격 저장소가 없습니다.' }

    Write-Host 'git add .'
    & git add .
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ('git commit -m "{0}"' -f $message)
    & git commit -m $message
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ('git push origin {0}' -f $branch)
    & git push origin $branch
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} catch {
    [Console]::Error.WriteLine('tool git 오류: ' + $_.Exception.Message)
    exit 1
}
