using System.Diagnostics;
using System.IO;

namespace ToolSlice;

public sealed class ToolCommandService
{
    private readonly string _root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    public string WorkingDirectory { get; } = Environment.CurrentDirectory;
    public string Root => _root;

    public Process StartPowerShell(string script, params string[] arguments) => Start("powershell.exe", ["-NoProfile", "-STA", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(_root, script), .. arguments]);

    public Process StartAlarm(string name, string mode) => Start("dotnet", ["run", "--project", Path.Combine(_root, "TaskAlarm.csproj"), "--no-launch-profile", "--", name, mode]);

    private Process Start(string file, string[] arguments)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new InvalidOperationException($"{file}을(를) 실행할 수 없습니다.");
    }
}
