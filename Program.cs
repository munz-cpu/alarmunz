using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

internal static partial class Program
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly string GlobalEditorLog = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Unity", "Editor", "Editor.log");

    private static async Task Main()
    {
        Console.WriteLine("=== Task Alarm ===");
        Console.WriteLine("1. 프로세스 종료 알람");
        Console.WriteLine("2. Unity 프로젝트 로딩 완료 알람");
        Console.Write("모드 선택 (1/2): ");
        var mode = Console.ReadLine()?.Trim();
        if (mode is not ("1" or "2")) return;

        var soundDirectory = Path.Combine(AppContext.BaseDirectory, "sounds",
            mode == "1" ? "default" : "unity");
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

        try
        {
            if (mode == "1") await WatchProcessExit(soundDirectory, cancellation.Token);
            else await WatchUnity(soundDirectory, cancellation.Token);
        }
        catch (OperationCanceledException) { }
    }

    private static async Task WatchProcessExit(string soundDirectory, CancellationToken cancellation)
    {
        Console.Write("감시할 프로세스 이름 (.exe 제외): ");
        var name = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
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
            if (unbound.Length == 1 && TryGetRedirectedLog(out var logPath, out var logWriteTime))
            {
                var session = unbound[0];
                if (logWriteTime >= session.StartTime.AddSeconds(-3) && File.Exists(logPath))
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
                    // Read from the beginning to support attaching while Unity is already loading.
                    // A ProjectInfo record after the completion line confirms the process identity.
                    foreach (var line in session.ReadNewLines())
                    {
                        if (line.StartsWith("[Project] Loading completed in ", StringComparison.Ordinal))
                        {
                            session.CompletionSeen = true;
                            Console.WriteLine($"[Unity {session.Pid}] 로딩 완료 신호 확인, PID 검증 중...");
                        }
                        else if (session.CompletionSeen && IsMatchingProjectInfo(line, session))
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

    private static bool IsMatchingProjectInfo(string line, UnitySession session)
    {
        if (!line.StartsWith("##utp:", StringComparison.Ordinal)) return false;
        try
        {
            using var document = JsonDocument.Parse(line[6..]);
            var root = document.RootElement;
            return root.GetProperty("type").GetString() == "ProjectInfo"
                && root.GetProperty("processId").GetInt32() == session.Pid
                && DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("time").GetInt64()).LocalDateTime >= session.StartTime;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private sealed class UnitySession(int pid, DateTime startTime)
    {
        public int Pid { get; } = pid;
        public DateTime StartTime { get; } = startTime;
        public string? LogPath { get; set; }
        public bool CompletionSeen { get; set; }
        public bool Notified { get; set; }
        private long _offset;
        private string _remainder = "";

        public IEnumerable<string> ReadNewLines()
        {
            using var stream = new FileStream(LogPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _offset) { _offset = 0; _remainder = ""; CompletionSeen = false; }
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
