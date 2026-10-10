using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Panel = System.Windows.Controls.Panel;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;

namespace ToolSlice;

public sealed class ToolHubWindow : Window
{
    private readonly ToolCommandService _commands = new();
    private readonly Action _selectRegion;
    private readonly Action<WindowTarget> _openPip;
    private readonly ComboBox _windows = new() { Width = 320, Margin = new Thickness(4), DisplayMemberPath = "Title" };
    private readonly TextBox _output = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 125 };
    private readonly TextBox _alarmName = Input("프로세스 이름", 160);
    private readonly ComboBox _alarmMode = new() { Width = 120, Margin = new Thickness(4) };
    private readonly TextBox _commitMessage = Input("커밋 메시지", 190);
    private readonly TextBox _branch = Input("브랜치", 120);
    private Process? _alarm;

    public ToolHubWindow(Action selectRegion, Action<WindowTarget> openPip)
    {
        _selectRegion = selectRegion;
        _openPip = openPip;
        Title = "Tool · 도구 모음";
        var logo = new BitmapImage(new Uri("pack://application:,,,/ToolSlice;component/Assets/logo.png"));
        Icon = logo;
        Width = 620;
        Height = 560;
        MinWidth = 500;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 249));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        var outer = new DockPanel { Margin = new Thickness(18) };
        var heading = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 8) };
        heading.Children.Add(new Image { Source = logo, Width = 40, Height = 40, Margin = new Thickness(0, 0, 10, 0) });
        heading.Children.Add(new TextBlock { Text = "Tool", FontSize = 26, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(heading, Dock.Top);
        outer.Children.Add(heading);
        var hint = new TextBlock { Text = "자주 쓰는 도구를 여기서 실행하세요.", Foreground = Brushes.DimGray, Margin = new Thickness(2, 0, 0, 14) };
        DockPanel.SetDock(hint, Dock.Top);
        outer.Children.Add(hint);
        var outputPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        outputPanel.Children.Add(new TextBlock { Text = "실행 결과", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 5) });
        outputPanel.Children.Add(_output);
        DockPanel.SetDock(outputPanel, Dock.Bottom);
        outer.Children.Add(outputPanel);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var sections = new StackPanel();
        scroll.Content = sections;
        outer.Children.Add(scroll);
        Content = outer;

        AddSection(sections, "창 PIP", Row(_windows,
            ActionButton("새로고침", RefreshWindows),
            ActionButton("선택한 창 띄우기", OpenSelectedWindow)));
        AddSection(sections, "화면", Row(
            ActionButton("창 일부 선택", () => { WindowState = WindowState.Minimized; _selectRegion(); }),
            ActionButton("색상 선택", async () => { WindowState = WindowState.Minimized; await RunAsync(() => _commands.StartPowerShell("ColorPicker.ps1")); WindowState = WindowState.Normal; Activate(); })));
        AddSection(sections, "빠른 도구", Row(
            ActionButton("클립보드 기록", async () => await RunAsync(() => _commands.StartPowerShell("Clipboard.ps1"))),
            ActionButton("주사위 굴리기", () => Log($"주사위: {Random.Shared.Next(1, 7)}"))));
        _alarmMode.ItemsSource = new[] { "on", "off", "loading" };
        _alarmMode.SelectedIndex = 0;
        AddSection(sections, "프로세스 알람", Row(_alarmName, _alarmMode,
            ActionButton("시작", StartAlarm), ActionButton("중지", StopAlarm)));
        AddSection(sections, "Git", Row(_commitMessage, _branch,
            ActionButton("Push", Push)));
        AddSection(sections, "Git Pull", Row(
            ActionButton("원격 main 가져오기", Pull)));
        _output.Text = $"준비됨 · 작업 폴더: {_commands.WorkingDirectory}{Environment.NewLine}";
        RefreshWindows();
        Closed += (_, _) => StopAlarm();
    }

    private void RefreshWindows()
    {
        _windows.ItemsSource = WindowCatalog.List();
        if (_windows.Items.Count > 0) _windows.SelectedIndex = 0;
    }

    private void OpenSelectedWindow()
    {
        if (_windows.SelectedItem is not WindowTarget target) { Log("원본 창을 선택하세요."); return; }
        try { _openPip(WindowTarget.Whole(target.Handle)); }
        catch (Exception ex) { Log($"PIP 생성 실패: {ex.Message}"); }
    }

    private static TextBox Input(string hint, double width = 0)
    {
        var box = new TextBox { Margin = new Thickness(4), Padding = new Thickness(6, 4, 6, 4), ToolTip = hint };
        if (width > 0) box.Width = width;
        else { box.MinWidth = 160; box.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch; }
        return box;
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(4), Padding = new Thickness(10, 6, 10, 6), MinHeight = 32 };
        button.Click += (_, _) => action();
        return button;
    }

    private static UIElement Row(params UIElement[] children)
    {
        var panel = new WrapPanel();
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private static void AddSection(Panel parent, string name, UIElement content)
    {
        var border = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(224, 228, 234)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 4, 10), Padding = new Thickness(10) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 0, 5) });
        stack.Children.Add(content);
        border.Child = stack;
        parent.Children.Add(border);
    }

    private async void StartAlarm()
    {
        if (_alarm is { HasExited: false }) { Log("알람이 이미 실행 중입니다."); return; }
        var name = _alarmName.Text.Trim();
        if (name.Length == 0) { Log("프로세스 이름을 입력하세요."); return; }
        try
        {
            _alarm = _commands.StartAlarm(name, (string)_alarmMode.SelectedItem);
            Log($"알람 시작: {name} ({_alarmMode.SelectedItem})");
            var process = _alarm;
            _ = ObserveAlarmAsync(process);
        }
        catch (Exception ex) { Log($"알람 실행 실패: {ex.Message}"); }
    }

    private async Task ObserveAlarmAsync(Process process)
    {
        try
        {
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) Dispatcher.Invoke(() => Log(e.Data)); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Dispatcher.Invoke(() => Log(e.Data)); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            Log($"알람 종료 (코드 {process.ExitCode})");
        }
        catch (Exception ex) { Log($"알람 오류: {ex.Message}"); }
        finally { if (ReferenceEquals(_alarm, process)) _alarm = null; process.Dispose(); }
    }

    private void StopAlarm()
    {
        if (_alarm is not { HasExited: false } process) return;
        try { process.Kill(entireProcessTree: true); Log("알람을 중지했습니다."); }
        catch (Exception ex) { Log($"알람 중지 실패: {ex.Message}"); }
    }

    private async void Push()
    {
        var message = _commitMessage.Text.Trim();
        var branch = _branch.Text.Trim();
        if (message.Length == 0 || branch.Length == 0) { Log("커밋 메시지와 브랜치를 입력하세요."); return; }
        if (MessageBox.Show(this, $"현재 작업 폴더에서 변경 내용을 커밋하고 origin/{branch}에 push할까요?", "Git Push 확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await RunAsync(() => _commands.StartPowerShell("Git.ps1", "git", "push", message, branch));
    }

    private async void Pull()
    {
        if (MessageBox.Show(this, "기존 tool git pull 명령은 추적된 로컬 변경을 초기화한 뒤 origin/main을 가져옵니다. 계속할까요?", "Git Pull 확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await RunAsync(() => _commands.StartPowerShell("Git.ps1", "git", "pull"));
    }

    private async Task RunAsync(Func<Process> start)
    {
        try
        {
            using var process = start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var result = (await stdout + await stderr).Trim();
            if (result.Length > 0) Log(result);
            Log($"완료 (코드 {process.ExitCode})");
        }
        catch (Exception ex) { Log($"실행 실패: {ex.Message}"); }
    }

    private void Log(string text)
    {
        _output.AppendText(text + Environment.NewLine);
        _output.ScrollToEnd();
    }
}
