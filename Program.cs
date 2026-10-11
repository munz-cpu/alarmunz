using System.Diagnostics;
using System.Text;
using NAudio.Wave;

internal static partial class Program
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly string GlobalEditorLog = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Unity", "Editor", "Editor.log");

    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && (args[0].Equals("alarm", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("a", StringComparison.OrdinalIgnoreCase))) args = args[1..];
        if (args.Length is < 1 or > 2 || string.IsNullOrWhiteSpace(args[0]))
        {
            ShowUsage();
            return 1;
        }

        var name = Path.GetFileNameWithoutExtension(args[0].Trim());
        var mode = args.Length == 2 ? args[1].ToLowerInvariant() : "loading";
        if (mode is not ("on" or "off" or "loading"))
        {
            ShowUsage();
            return 1;
        }
        if (mode == "loading" && !name.Equals("Unity", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("loading 모드는 현재 Unity만 지원합니다. on 또는 off를 사용하세요.");
            return 1;
        }

        var soundDirectory = Path.Combine(AppContext.BaseDirectory, "sounds",
            mode == "loading" ? "unity" : "default");
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

        try
        {
            if (mode == "on") await WatchProcessStart(name, soundDirectory, cancellation.Token);
            else if (mode == "off") await WatchProcessExit(name, soundDirectory, cancellation.Token);
            else await WatchUnity(soundDirectory, cancellation.Token);
        }
        catch (OperationCanceledException) { }
        return 0;
    }

    private static void ShowUsage() => Console.WriteLine("사용법: tool alarm [프로그램 이름] [on|off|loading] (기본값: loading)");

    private static async Task WatchProcessStart(string name, string soundDirectory, CancellationToken cancellation)
    {
        Console.WriteLine($"{name} 프로세스 시작을 기다리는 중... (종료: Ctrl+C)");
        while (true)
        {
            var processes = Process.GetProcessesByName(name);
            if (processes.Length > 0)
            {
                foreach (var process in processes) process.Dispose();
                Console.WriteLine($"{name} 프로세스가 실행되었습니다!");
                await PlayAlarm(soundDirectory, cancellation);
                return;
            }
            await Task.Delay(PollInterval, cancellation);
        }
    }

    private static async Task WatchProcessExit(string name, string soundDirectory, CancellationToken cancellation)
    {
        Console.WriteLine($"{name} 프로세스를 기다리는 중... (종료: Ctrl+C)");
        Process? process = null;
        while (process is null)
        {
            process = Process.GetProcessesByName(name).FirstOrDefault();
            if (process is null) await Task.Delay(PollInterval, cancellation);
        }
        using (process)
        {
            Console.WriteLine($"프로세스 감지! PID: {process.Id}");
            Console.WriteLine("프로세스 종료를 기다리는 중... (종료: Ctrl+C)");
            await process.WaitForExitAsync(cancellation);
        }
        Console.WriteLine("프로세스가 종료되었습니다!");
        await PlayAlarm(soundDirectory, cancellation);
    }

    private static async Task WatchUnity(string soundDirectory, CancellationToken cancellation)
    {
        Console.WriteLine("Unity 에디터 실행을 기다리는 중... (종료: Ctrl+C)");
        var tracked = new Dictionary<int, UnitySession>();
        while (true)
        {
            var active = new HashSet<int>();
            foreach (var process in Process.GetProcessesByName("Unity"))
            {
                using (process)
                {
                    try
                    {
                        if (process.HasExited) continue;
                        active.Add(process.Id);
                        if (!tracked.TryGetValue(process.Id, out var session) || session.StartTime != process.StartTime)
                        {
                            session = new UnitySession(process.Id, process.StartTime);
                            tracked[process.Id] = session;
                            Console.WriteLine($"[Unity {process.Id}] 실행 감지, 로딩 중... (종료: Ctrl+C)");
                        }
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        Console.WriteLine($"[Unity] 프로세스 정보 조회 실패: {ex.Message}");
                    }
                }
            }

            foreach (var pid in tracked.Keys.Where(pid => !active.Contains(pid)).ToArray())
            {
                Console.WriteLine($"[Unity {pid}] 종료 감지");
                tracked.Remove(pid);
            }

            // Unity 6 on this machine redirects the startup log to <project>/Logs/Editor.log.
            // A single global redirect is unambiguous only while one unbound editor exists.
            var unbound = tracked.Values.Where(s => s.LogPath is null).ToArray();
            if (unbound.Length == 1 && File.Exists(GlobalEditorLog))
            {
                var session = unbound[0];
                var logPath = GlobalEditorLog;
                if (TryGetRedirectedLog(out var redirectedPath, out var logWriteTime)
                    && File.Exists(redirectedPath))
                    logPath = redirectedPath;
                else
                    logWriteTime = File.GetLastWriteTime(GlobalEditorLog);

                if (logWriteTime >= session.StartTime.AddSeconds(-3))
                {
                    session.LogPath = logPath;
                    Console.WriteLine($"[Unity {session.Pid}] 프로젝트 로그 연결: {logPath}");
                }
            }

            foreach (var session in tracked.Values)
            {
                if (session.LogPath is null || session.Notified) continue;
                try
                {
                    // Read from the beginning to support attaching to an editor that is already open.
                    // The log was matched to this editor by its startup time above.
                    foreach (var line in session.ReadNewLines())
                    {
                        if (line.StartsWith("[Project] Loading completed in ", StringComparison.Ordinal))
                        {
                            session.Notified = true;
                            Console.WriteLine($"[Unity {session.Pid}] 프로젝트 로딩 완료: {session.LogPath}");
                            await PlayAlarm(soundDirectory, cancellation);
                            break;
                        }
                    }
                }
                catch (IOException ex) { Console.WriteLine($"[Unity {session.Pid}] 로그 읽기 재시도: {ex.Message}"); }
                catch (UnauthorizedAccessException ex) { Console.WriteLine($"[Unity {session.Pid}] 로그 접근 실패: {ex.Message}"); }
            }
            await Task.Delay(PollInterval, cancellation);
        }
    }

    private static bool TryGetRedirectedLog(out string path, out DateTime writeTime)
    {
        path = "";
        writeTime = DateTime.MinValue;
        try
        {
            if (!File.Exists(GlobalEditorLog)) return false;
            writeTime = File.GetLastWriteTime(GlobalEditorLog);
            using var reader = new StreamReader(GlobalEditorLog);
            while (reader.ReadLine() is { } line)
            {
                const string prefix = "Logs moved to project-relative Editor.log file, and subsequent logs will only be written to this file. See:";
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var next = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(next)) path = next.Trim().Replace('/', Path.DirectorySeparatorChar);
            }
            return path.Length > 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private sealed class UnitySession(int pid, DateTime startTime)
    {
        public int Pid { get; } = pid;
        public DateTime StartTime { get; } = startTime;
        public string? LogPath { get; set; }
        public bool Notified { get; set; }
        private long _offset;
        private string _remainder = "";

        public IEnumerable<string> ReadNewLines()
        {
            using var stream = new FileStream(LogPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _offset) { _offset = 0; _remainder = ""; }
            stream.Seek(_offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var chunk = reader.ReadToEnd();
            _offset = stream.Position;
            var content = _remainder + chunk;
            var lastNewline = content.LastIndexOf('\n');
            if (lastNewline < 0) { _remainder = content; yield break; }
            _remainder = content[(lastNewline + 1)..];
            foreach (var line in content[..(lastNewline + 1)].Split('\n'))
                if (line.Length > 0) yield return line.TrimEnd('\r');
        }
    }

    private static async Task PlayAlarm(string soundDirectory, CancellationToken cancellation)
    {
        try
        {
            var files = Directory.Exists(soundDirectory)
                ? Directory.EnumerateFiles(soundDirectory, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase)
                        || Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                    .ToArray()
                : [];
            if (files.Length == 0)
            {
                Console.WriteLine($"재생할 WAV 또는 MP3 파일이 없습니다: {soundDirectory}");
                return;
            }
            var soundPath = files[Random.Shared.Next(files.Length)];
            Console.WriteLine($"알림음 재생: {Path.GetFileName(soundPath)}");
            using var audioFile = new AudioFileReader(soundPath);
            using var output = new WaveOut();
            output.Init(audioFile);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
                await Task.Delay(100, cancellation);
        }
        catch (Exception ex) { Console.WriteLine($"알람 오류: {ex.Message}"); }
    }
}
