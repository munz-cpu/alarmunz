$ErrorActionPreference = 'Stop'

function Await-WinRtOperation($operation, [Type]$resultType) {
    $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 } |
        Select-Object -First 1
    $task = $asTask.MakeGenericMethod($resultType).Invoke($null, @($operation))
    return $task.GetAwaiter().GetResult()
}

try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $null = [Windows.ApplicationModel.DataTransfer.Clipboard, Windows.ApplicationModel.DataTransfer, ContentType=WindowsRuntime]
    $null = [Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResult, Windows.ApplicationModel.DataTransfer, ContentType=WindowsRuntime]
    $null = [Windows.ApplicationModel.DataTransfer.StandardDataFormats, Windows.ApplicationModel.DataTransfer, ContentType=WindowsRuntime]

    $operation = [Windows.ApplicationModel.DataTransfer.Clipboard]::GetHistoryItemsAsync()
    $result = Await-WinRtOperation $operation ([Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResult])
    if ($result.Status -ne 'Success') {
        Write-Host ('클립보드 기록을 읽을 수 없습니다 ({0}). Win+V에서 기록이 켜져 있는지, 현재 세션에서 접근할 수 있는지 확인해 주세요.' -f $result.Status)
        exit 1
    }

    $count = [Math]::Min(10, $result.Items.Count)
    if ($count -eq 0) {
        Write-Host '클립보드 기록이 비어 있습니다.'
        exit 0
    }

    $textFormat = [Windows.ApplicationModel.DataTransfer.StandardDataFormats]::Text
    for ($i = 0; $i -lt $count; $i++) {
        $content = $result.Items[$i].Content
        if ($content.Contains($textFormat)) {
            $text = Await-WinRtOperation ($content.GetTextAsync()) ([string])
            Write-Host ('{0}. {1}' -f ($i + 1), $text)
        } else {
            Write-Host ('{0}. [텍스트가 아닌 항목]' -f ($i + 1))
        }
    }
} catch {
    [Console]::Error.WriteLine('클립보드 기록 조회 실패: ' + $_.Exception.Message)
    exit 1
}
