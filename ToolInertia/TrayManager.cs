namespace ToolInertia;

internal sealed class TrayManager : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly InertiaEngine _engine;
    private readonly WindowTracker _tracker;
    private readonly MouseHook _mouse;
    private readonly NotifyIcon _icon;
    private SettingsForm? _form;

    internal TrayManager()
    {
        _settings = SettingsManager.Load();
        _engine = new InertiaEngine(() => _settings);
        _tracker = new WindowTracker(_engine, () => _settings);
        _mouse = new MouseHook();
        _mouse.Released += _tracker.Released;
        _mouse.Pressed += _tracker.Pressed;
        var menu = new ContextMenuStrip();
        var enabled = new ToolStripMenuItem("관성 ON") { Checked = _settings.Enabled, CheckOnClick = true };
        enabled.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = enabled.Checked;
            enabled.Text = _settings.Enabled ? "관성 ON" : "관성 OFF";
            if (!_settings.Enabled) _tracker.CancelAll();
            try { SettingsManager.Save(_settings); } catch (Exception ex) { ShowError(ex); }
        };
        menu.Items.Add(enabled);
        menu.Items.Add("설정 열기", null, (_, _) => OpenSettings());
        menu.Items.Add("종료", null, (_, _) => ExitThread());
        _icon = new NotifyIcon { Icon = SystemIcons.Application, Text = "Tool Inertia", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => OpenSettings();
    }

    private void OpenSettings()
    {
        if (_form is { IsDisposed: false }) { _form.Activate(); return; }
        _form = new SettingsForm(_settings);
        _form.Show();
        _form.Activate();
    }

    private static void ShowError(Exception ex) =>
        MessageBox.Show(ex.Message, "Tool Inertia", MessageBoxButtons.OK, MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _mouse.Dispose();
            _tracker.Dispose();
            _engine.Dispose();
            _form?.Dispose();
        }
        base.Dispose(disposing);
    }
}
