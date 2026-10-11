using System.Text.Json;
using Microsoft.Win32;

namespace ToolInertia;

internal static class SettingsManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "ToolInertia";
    internal static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolInertia", "settings.json");

    internal static AppSettings Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
            if (settings.SettingsVersion < 2)
            {
                // Migrate only untouched original defaults; keep user-adjusted values.
                if (settings.Strength == 1.0) settings.Strength = 1.4;
                if (settings.Friction == 5.0) settings.Friction = 3.0;
                if (settings.MinimumSpeed == 180.0) settings.MinimumSpeed = 140.0;
                settings.SettingsVersion = 2;
            }
            settings.Clamp();
            return settings;
        }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
        catch (JsonException) { return new(); }
    }

    internal static void Save(AppSettings settings)
    {
        settings.Clamp();
        settings.SettingsVersion = 2;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }

    internal static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunName) is string;
    }

    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(RunName, $"\"{Application.ExecutablePath}\"");
        else key.DeleteValue(RunName, false);
    }
}
