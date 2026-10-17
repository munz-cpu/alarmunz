using System.Diagnostics;

namespace ToolInertia;

internal sealed class WindowTracker : IDisposable
{
    private sealed class Drag
    {
        internal required WindowIdentity Identity;
        internal required Native.Rect Initial;
        internal readonly List<Sample> Samples = [];
        internal int LastX, LastY;
        internal bool Resized;
        internal long ReleaseStamp;
    }
    private readonly record struct Sample(long Stamp, int X, int Y);
    private readonly Native.WinEventProc _callback;
    private readonly System.Windows.Forms.Timer _sampleTimer = new() { Interval = 8 };
    private readonly Dictionary<nint, Drag> _drags = [];
    private nint _moveHook, _destroyHook;
    private readonly InertiaEngine _engine;
    private readonly Func<AppSettings> _settings;

    internal WindowTracker(InertiaEngine engine, Func<AppSettings> settings)
    {
        _engine = engine;
        _settings = settings;
        _callback = OnEvent;
        _sampleTimer.Tick += (_, _) => SampleAll();
        _moveHook = Native.SetWinEventHook(Native.MoveStart, Native.MoveEnd, 0, _callback, 0, 0,
            Native.OutOfContext | Native.SkipOwnProcess);
        _destroyHook = Native.SetWinEventHook(Native.ObjectDestroy, Native.ObjectDestroy, 0, _callback, 0, 0,
            Native.OutOfContext | Native.SkipOwnProcess);
        if (_moveHook == 0 || _destroyHook == 0) throw new InvalidOperationException("Windows event hook could not be installed.");
    }

    internal void Released()
    {
        var now = Stopwatch.GetTimestamp();
        foreach (var drag in _drags.Values) drag.ReleaseStamp = now;
    }

    internal void Pressed(Native.Point point)
    {
        var root = Native.GetAncestor(Native.WindowFromPoint(point), Native.GaRoot);
        if (root != 0) _engine.Cancel(root);
    }

    internal void CancelAll()
    {
        _drags.Clear();
        _sampleTimer.Stop();
        _engine.CancelAll();
    }

    private void OnEvent(nint hook, uint evt, nint hwnd, int objectId, int childId, uint threadId, uint time)
    {
        if (objectId != Native.ObjIdWindow || childId != 0 || hwnd == 0) return;
        try
        {
            if (evt == Native.ObjectDestroy) { _drags.Remove(hwnd); _engine.Cancel(hwnd); return; }
            if (evt == Native.MoveStart) Begin(hwnd);
            else if (evt == Native.MoveEnd) End(hwnd);
        }
        catch { _drags.Remove(hwnd); _engine.Cancel(hwnd); }
    }

    private void Begin(nint hwnd)
    {
        _engine.Cancel(hwnd);
        if (!_settings().Enabled || Native.GetAsyncKeyState(Native.VkLButton) >= 0 || !WindowMover.Eligible(hwnd)) return;
        var id = WindowMover.Identify(hwnd);
        if (id is null || !Native.GetWindowRect(hwnd, out var rect)) return;
        var drag = new Drag { Identity = id.Value, Initial = rect, LastX = rect.Left, LastY = rect.Top };
        drag.Samples.Add(new(Stopwatch.GetTimestamp(), rect.Left, rect.Top));
        _drags[hwnd] = drag;
        _sampleTimer.Start();
    }

    private void SampleAll()
    {
        var now = Stopwatch.GetTimestamp();
        foreach (var (hwnd, drag) in _drags.ToArray())
        {
            if (!WindowMover.Same(drag.Identity) || !Native.GetWindowRect(hwnd, out var rect))
            { _drags.Remove(hwnd); continue; }
            if (rect.Width != drag.Initial.Width || rect.Height != drag.Initial.Height) drag.Resized = true;
            if (!drag.Resized && _settings().Enabled)
            {
                var previous = rect;
                previous.Right += drag.LastX - previous.Left;
                previous.Bottom += drag.LastY - previous.Top;
                previous.Left = drag.LastX;
                previous.Top = drag.LastY;
                var bounded = MonitorManager.Constrain(previous, rect.Left, rect.Top);
                var x = (int)Math.Round(bounded.X);
                var y = (int)Math.Round(bounded.Y);
                if ((x != rect.Left || y != rect.Top) && WindowMover.Move(drag.Identity, x, y))
                {
                    rect.Right += x - rect.Left;
                    rect.Bottom += y - rect.Top;
                    rect.Left = x;
                    rect.Top = y;
                }
                drag.LastX = rect.Left;
                drag.LastY = rect.Top;
            }
            if (drag.Samples.Count == 0 || drag.Samples[^1].X != rect.Left || drag.Samples[^1].Y != rect.Top)
                drag.Samples.Add(new(now, rect.Left, rect.Top));
            drag.Samples.RemoveAll(s => (now - s.Stamp) / (double)Stopwatch.Frequency > .25);
        }
        if (_drags.Count == 0) _sampleTimer.Stop();
    }

    private void End(nint hwnd)
    {
        if (!_drags.Remove(hwnd, out var drag)) return;
        if (_drags.Count == 0) _sampleTimer.Stop();
        var now = Stopwatch.GetTimestamp();
        if (_settings().Enabled && !drag.Resized && WindowMover.Same(drag.Identity) &&
            Native.GetWindowRect(hwnd, out var finalRect) &&
            finalRect.Width == drag.Initial.Width && finalRect.Height == drag.Initial.Height)
        {
            var previous = finalRect;
            previous.Right += drag.LastX - previous.Left;
            previous.Bottom += drag.LastY - previous.Top;
            previous.Left = drag.LastX;
            previous.Top = drag.LastY;
            var bounded = MonitorManager.Constrain(previous, finalRect.Left, finalRect.Top);
            WindowMover.Move(drag.Identity, (int)Math.Round(bounded.X), (int)Math.Round(bounded.Y));
        }
        // MoveEnd is sent after Windows has completed its own drag loop.
        if (!_settings().Enabled || drag.Resized || drag.ReleaseStamp == 0 ||
            (now - drag.ReleaseStamp) / (double)Stopwatch.Frequency > .35 ||
            Native.GetAsyncKeyState(Native.VkLButton) < 0 ||
            !WindowMover.Same(drag.Identity) || !WindowMover.Eligible(hwnd) ||
            !Native.GetWindowRect(hwnd, out var rect) ||
            rect.Width != drag.Initial.Width || rect.Height != drag.Initial.Height) return;
        var movement = Math.Abs(rect.Left - drag.Initial.Left) + Math.Abs(rect.Top - drag.Initial.Top);
        if (movement < 8) return;
        // Use a sample roughly 100 ms before the final observed motion.
        var last = drag.Samples.LastOrDefault();
        if (last.Stamp == 0 || (now - last.Stamp) / (double)Stopwatch.Frequency > .12) return;
        var earlier = drag.Samples.Where(s => s.Stamp < last.Stamp &&
            (last.Stamp - s.Stamp) / (double)Stopwatch.Frequency <= .12).FirstOrDefault();
        if (earlier.Stamp == 0) return;
        var dt = (last.Stamp - earlier.Stamp) / (double)Stopwatch.Frequency;
        if (dt < .015) return;
        var vx = (last.X - earlier.X) / dt;
        var vy = (last.Y - earlier.Y) / dt;
        _engine.Start(drag.Identity, rect, vx, vy);
    }

    public void Dispose()
    {
        CancelAll();
        if (_moveHook != 0) Native.UnhookWinEvent(_moveHook);
        if (_destroyHook != 0) Native.UnhookWinEvent(_destroyHook);
        _sampleTimer.Dispose();
    }
}
