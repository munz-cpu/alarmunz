namespace ToolInertia;

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public int SettingsVersion { get; set; }
    public double Strength { get; set; } = 1.4;
    public double Friction { get; set; } = 3.0;
    public double MinimumSpeed { get; set; } = 140.0;
    public double MaximumSpeed { get; set; } = 3000.0;
    public int UpdateIntervalMs { get; set; } = 16;
    public bool StartWithWindows { get; set; }

    public void Clamp()
    {
        Strength = Math.Clamp(Strength, 0.1, 3);
        Friction = Math.Clamp(Friction, 1, 15);
        MinimumSpeed = Math.Clamp(MinimumSpeed, 20, 1000);
        MaximumSpeed = Math.Clamp(MaximumSpeed, 500, 6000);
        UpdateIntervalMs = Math.Clamp(UpdateIntervalMs, 8, 50);
    }
}
