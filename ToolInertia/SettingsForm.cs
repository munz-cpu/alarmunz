namespace ToolInertia;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _strength = Number(0.1m, 3, .1m, 2);
    private readonly NumericUpDown _friction = Number(1, 15, .1m, 1);
    private readonly NumericUpDown _minimum = Number(20, 1000, 10, 0);
    private readonly NumericUpDown _maximum = Number(500, 6000, 100, 0);
    private readonly NumericUpDown _interval = Number(8, 50, 1, 0);
    private readonly CheckBox _startup = new() { Text = "Windows 시작 시 실행", AutoSize = true };
    private readonly AppSettings _settings;

    internal SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "Tool Inertia 설정";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(390, 320);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        AddRow(layout, 0, "관성 강도 (배)", _strength, (decimal)settings.Strength);
        AddRow(layout, 1, "마찰력 (1/초)", _friction, (decimal)settings.Friction);
        AddRow(layout, 2, "최소 속도 (px/초)", _minimum, (decimal)settings.MinimumSpeed);
        AddRow(layout, 3, "최대 속도 (px/초)", _maximum, (decimal)settings.MaximumSpeed);
        AddRow(layout, 4, "업데이트 주기 (ms)", _interval, settings.UpdateIntervalMs);
        _startup.Checked = SettingsManager.IsStartupEnabled();
        layout.Controls.Add(_startup, 0, 5);
        layout.SetColumnSpan(_startup, 2);
        var save = new Button { Text = "저장", AutoSize = true, Anchor = AnchorStyles.Right };
        save.Click += (_, _) => Save();
        layout.Controls.Add(save, 1, 6);
        Controls.Add(layout);
        AcceptButton = save;
    }

    private static NumericUpDown Number(decimal min, decimal max, decimal step, int decimals) =>
        new() { Minimum = min, Maximum = max, Increment = step, DecimalPlaces = decimals, Dock = DockStyle.Fill };

    private static void AddRow(TableLayoutPanel layout, int row, string label, NumericUpDown input, decimal value)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        input.Value = value;
        layout.Controls.Add(input, 1, row);
    }

    private void Save()
    {
        try
        {
            SettingsManager.SetStartup(_startup.Checked);
            _settings.Strength = (double)_strength.Value;
            _settings.Friction = (double)_friction.Value;
            _settings.MinimumSpeed = (double)_minimum.Value;
            _settings.MaximumSpeed = (double)_maximum.Value;
            _settings.UpdateIntervalMs = (int)_interval.Value;
            _settings.StartWithWindows = _startup.Checked;
            SettingsManager.Save(_settings);
            Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "설정 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
