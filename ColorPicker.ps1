$ErrorActionPreference = 'Stop'

if ($args.Count -gt 0) {
    if ($args.Count -eq 1 -and $args[0] -in @('help', '--help', '-h')) {
        Write-Host 'Usage: tool colorpicker'
        Write-Host 'A live preview shows the current pixel. Click the screen to copy its HEX color, or press Esc to cancel.'
        exit 0
    }
    [Console]::Error.WriteLine('Usage: tool colorpicker')
    exit 1
}

try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class ScreenColorPicker {
    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")]
    public static extern uint GetPixel(IntPtr dc, int x, int y);
}
'@

    $pickerForm = [System.Windows.Forms.Form]::new()
    $pickerForm.Text = '색상 선택 중'
    $pickerForm.ClientSize = [System.Drawing.Size]::new(260, 105)
    $pickerForm.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedToolWindow
    $pickerForm.ControlBox = $false
    $pickerForm.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $pickerForm.TopMost = $true
    $workArea = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $pickerForm.Location = [System.Drawing.Point]::new(($workArea.Right - 280), ($workArea.Bottom - 135))

    $instruction = [System.Windows.Forms.Label]::new()
    $instruction.Text = '색상 선택 중 · 화면을 클릭하세요  (Esc 취소)'
    $instruction.Location = [System.Drawing.Point]::new(12, 12)
    $instruction.Size = [System.Drawing.Size]::new(240, 25)
    $pickerForm.Controls.Add($instruction)

    $preview = [System.Windows.Forms.Panel]::new()
    $preview.Location = [System.Drawing.Point]::new(12, 48)
    $preview.Size = [System.Drawing.Size]::new(44, 36)
    $preview.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $pickerForm.Controls.Add($preview)

    $previewHex = [System.Windows.Forms.Label]::new()
    $previewHex.Text = '#------'
    $previewHex.Font = [System.Drawing.Font]::new('Segoe UI', 15, [System.Drawing.FontStyle]::Bold)
    $previewHex.Location = [System.Drawing.Point]::new(70, 50)
    $previewHex.Size = [System.Drawing.Size]::new(170, 35)
    $pickerForm.Controls.Add($previewHex)
    $pickerForm.Show()

    Write-Host '색상 선택 중: 화면 픽셀을 클릭하세요. 취소: Esc'
    $leftWasDown = ([ScreenColorPicker]::GetAsyncKeyState(0x01) -band 0x8000) -ne 0
    while ($true) {
        [System.Windows.Forms.Application]::DoEvents()
        if (([ScreenColorPicker]::GetAsyncKeyState(0x1B) -band 0x8000) -ne 0) {
            $pickerForm.Dispose()
            Write-Host '취소했습니다.'
            exit 0
        }
        $point = [ScreenColorPicker+Point]::new()
        $hasCursor = [ScreenColorPicker]::GetCursorPos([ref]$point)
        if ($hasCursor) {
            $cursorPoint = [System.Drawing.Point]::new($point.X, $point.Y)
            if (-not $pickerForm.Bounds.Contains($cursorPoint)) {
                $previewDc = [ScreenColorPicker]::GetDC([IntPtr]::Zero)
                if ($previewDc -ne [IntPtr]::Zero) {
                    try { $previewColor = [ScreenColorPicker]::GetPixel($previewDc, $point.X, $point.Y) }
                    finally { [void][ScreenColorPicker]::ReleaseDC([IntPtr]::Zero, $previewDc) }
                    if ($previewColor -ne [uint32]::MaxValue) {
                        $preview.BackColor = [System.Drawing.Color]::FromArgb(($previewColor -band 0xFF), (($previewColor -shr 8) -band 0xFF), (($previewColor -shr 16) -band 0xFF))
                        $previewHex.Text = '#{0:X2}{1:X2}{2:X2}' -f ($previewColor -band 0xFF), (($previewColor -shr 8) -band 0xFF), (($previewColor -shr 16) -band 0xFF)
                    }
                }
            }
        }
        $leftIsDown = ([ScreenColorPicker]::GetAsyncKeyState(0x01) -band 0x8000) -ne 0
        if ($leftIsDown -and -not $leftWasDown -and $hasCursor -and -not $pickerForm.Bounds.Contains($cursorPoint)) { break }
        $leftWasDown = $leftIsDown
        Start-Sleep -Milliseconds 50
    }

    $pickerForm.Dispose()

    $point = [ScreenColorPicker+Point]::new()
    if (-not [ScreenColorPicker]::GetCursorPos([ref]$point)) { throw '마우스 위치를 읽을 수 없습니다.' }
    $dc = [ScreenColorPicker]::GetDC([IntPtr]::Zero)
    if ($dc -eq [IntPtr]::Zero) { throw '화면 픽셀을 읽을 수 없습니다.' }
    try {
        $color = [ScreenColorPicker]::GetPixel($dc, $point.X, $point.Y)
    } finally {
        [void][ScreenColorPicker]::ReleaseDC([IntPtr]::Zero, $dc)
    }
    if ($color -eq [uint32]::MaxValue) { throw '선택한 픽셀의 색을 읽을 수 없습니다.' }

    $hex = '#{0:X2}{1:X2}{2:X2}' -f ($color -band 0xFF), (($color -shr 8) -band 0xFF), (($color -shr 16) -band 0xFF)
    [System.Windows.Forms.Clipboard]::SetText($hex)
    Write-Host ('{0} (클립보드에 복사됨)' -f $hex)

    $form = [System.Windows.Forms.Form]::new()
    $form.Text = 'Color Picker'
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
    $form.ClientSize = [System.Drawing.Size]::new(260, 145)
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $form.MaximizeBox = $false
    $form.TopMost = $true

    $swatch = [System.Windows.Forms.Panel]::new()
    $swatch.Location = [System.Drawing.Point]::new(20, 20)
    $swatch.Size = [System.Drawing.Size]::new(55, 55)
    $swatch.BackColor = [System.Drawing.Color]::FromArgb(($color -band 0xFF), (($color -shr 8) -band 0xFF), (($color -shr 16) -band 0xFF))
    $swatch.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $form.Controls.Add($swatch)

    $hexLabel = [System.Windows.Forms.Label]::new()
    $hexLabel.Text = $hex
    $hexLabel.Font = [System.Drawing.Font]::new('Segoe UI', 18, [System.Drawing.FontStyle]::Bold)
    $hexLabel.Location = [System.Drawing.Point]::new(90, 23)
    $hexLabel.Size = [System.Drawing.Size]::new(150, 40)
    $hexLabel.Cursor = [System.Windows.Forms.Cursors]::Hand
    $hexLabel.Add_Click({
        [System.Windows.Forms.Clipboard]::SetText($hexLabel.Text)
        $status.Text = '클립보드에 복사됨'
    })
    $form.Controls.Add($hexLabel)

    $status = [System.Windows.Forms.Label]::new()
    $status.Text = 'HEX를 클릭하면 복사됩니다'
    $status.Location = [System.Drawing.Point]::new(20, 100)
    $status.Size = [System.Drawing.Size]::new(220, 25)
    $form.Controls.Add($status)

    [void]$form.ShowDialog()
    $form.Dispose()
} catch {
    [Console]::Error.WriteLine('colorpicker 오류: ' + $_.Exception.Message)
    exit 1
}
