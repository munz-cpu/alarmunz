using System.Diagnostics;

namespace ToolInertia;

internal sealed class InertiaEngine : IDisposable
{
    private sealed class Motion
    {
        internal required WindowIdentity Identity;
        internal double X, Y, Vx, Vy;
        internal long Stamp;
    }
    private readonly Dictionary<nint, Motion> _motions = [];
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Func<AppSettings> _settings;

    internal InertiaEngine(Func<AppSettings> settings)
    {
        _settings = settings;
        _timer.Tick += (_, _) => Tick();
    }

    internal void Start(WindowIdentity id, Native.Rect rect, double vx, double vy)
    {
        var settings = _settings();
        if (!settings.Enabled) return;
        var speed = Math.Sqrt(vx * vx + vy * vy);
        if (speed < settings.MinimumSpeed || !double.IsFinite(speed)) return;
        var scale = Math.Min(speed, settings.MaximumSpeed) / speed * settings.Strength;
        _motions[id.Handle] = new Motion { Identity = id, X = rect.Left, Y = rect.Top,
            Vx = vx * scale, Vy = vy * scale, Stamp = Stopwatch.GetTimestamp() };
        _timer.Interval = settings.UpdateIntervalMs;
        _timer.Start();
    }

    private void Tick()
    {
        var now = Stopwatch.GetTimestamp();
        var friction = _settings().Friction;
        foreach (var (hwnd, motion) in _motions.ToArray())
        {
            try
            {
                if (!WindowMover.Same(motion.Identity) || !WindowMover.Eligible(hwnd) ||
                    !Native.GetWindowRect(hwnd, out var rect)) { _motions.Remove(hwnd); continue; }
                // Stop if another actor moved the window or changed its dimensions.
                if (Math.Abs(rect.Left - motion.X) > 4 || Math.Abs(rect.Top - motion.Y) > 4)
                { _motions.Remove(hwnd); continue; }
                var dt = Math.Clamp((now - motion.Stamp) / (double)Stopwatch.Frequency, 0, .05);
                motion.Stamp = now;
                var decay = Math.Exp(-friction * dt);
                var step = (1 - decay) / friction;
                var targetX = motion.X + motion.Vx * step;
                var targetY = motion.Y + motion.Vy * step;
                var constrained = MonitorManager.Constrain(rect, targetX, targetY);
                motion.X = constrained.X;
                motion.Y = constrained.Y;
                motion.Vx = constrained.StopX ? 0 : motion.Vx * decay;
                motion.Vy = constrained.StopY ? 0 : motion.Vy * decay;
                if (!WindowMover.Move(motion.Identity, (int)Math.Round(motion.X), (int)Math.Round(motion.Y)) ||
                    Math.Sqrt(motion.Vx * motion.Vx + motion.Vy * motion.Vy) < 20)
                    _motions.Remove(hwnd);
            }
            catch { _motions.Remove(hwnd); }
        }
        if (_motions.Count == 0) _timer.Stop();
    }

    internal void Cancel(nint hwnd)
    {
        _motions.Remove(hwnd);
        if (_motions.Count == 0) _timer.Stop();
    }

    internal void CancelAll()
    {
        _motions.Clear();
        _timer.Stop();
    }

    public void Dispose() { CancelAll(); _timer.Dispose(); }
}
