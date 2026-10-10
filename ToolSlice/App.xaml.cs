using System.Windows;
using Forms = System.Windows.Forms;

namespace ToolSlice;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _tray;
    private readonly List<PipWindow> _slices = [];
    private ToolHubWindow? _hub;
    private bool _selecting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShowHub();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("도구 창 열기", null, (_, _) => Dispatcher.Invoke(ShowHub));
        menu.Items.Add("화면 자르기", null, (_, _) => Dispatcher.Invoke(BeginSelection));
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(Shutdown));
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico")), Text = "Tool GUI", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowHub);
    }

    private void ShowHub()
    {
        if (_hub is null)
        {
            _hub = new ToolHubWindow(BeginSelection, OpenPip);
            _hub.Closed += (_, _) => _hub = null;
            MainWindow = _hub;
        }
        _hub.Show();
        _hub.WindowState = WindowState.Normal;
        _hub.Activate();
    }

    private void BeginSelection()
    {
        if (_selecting) return;
        _selecting = true;
        var overlay = new SelectionOverlay();
        overlay.Completed += region =>
        {
            _selecting = false;
            if (region is null) { ShowHub(); return; }
            var selection = region.Value.Bounds;
            var center = new System.Drawing.Point(selection.Left + selection.Width / 2, selection.Top + selection.Height / 2);
            var hwnd = WindowCatalog.WindowAt(center);
            if (hwnd == IntPtr.Zero)
            {
                System.Windows.MessageBox.Show("선택한 영역 아래에서 캡처 가능한 창을 찾지 못했습니다. 원본 창을 앞에 놓고 다시 선택하세요.", "Tool Slice");
                ShowHub();
                return;
            }
            try { OpenPip(WindowTarget.Region(hwnd, selection)); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Tool Slice"); ShowHub(); }
        };
        overlay.Show();
    }

    private void OpenPip(WindowTarget target)
    {
        var pip = new PipWindow(target);
        _slices.Add(pip);
        pip.Closed += (_, _) => _slices.Remove(pip);
        pip.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }
}
