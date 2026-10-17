using System.Diagnostics;

namespace ToolInertia;

internal sealed class InertiaEngine : IDisposable
{
    private sealed class Motion
    {
        internal required WindowIdentity Identity;
        internal double X, Y, Vx, Vy;
        internal double LaunchVx, LaunchVy;
        internal double BoundaryX, BoundaryY;
        internal double OvershootLimitX, OvershootLimitY;
        internal int SpringX, SpringY;
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
        var bounded = MonitorManager.Constrain(rect, rect.Left, rect.Top);
        if (bounded.BounceX || bounded.BounceY)
        {
            // The user may drag a window outside the display. Let the drag finish,
            // then pull it back without an instantaneous position correction.
            _motions[id.Handle] = new Motion
            {
                Identity = id, X = rect.Left, Y = rect.Top,
                BoundaryX = bounded.X, BoundaryY = bounded.Y,
                OvershootLimitX = double.PositiveInfinity,
                OvershootLimitY = double.PositiveInfinity,
                SpringX = bounded.BounceX ? Math.Sign(rect.Left - bounded.X) : 0,
                SpringY = bounded.BounceY ? Math.Sign(rect.Top - bounded.Y) : 0,
                Stamp = Stopwatch.GetTimestamp()
            };
            _timer.Interval = settings.UpdateIntervalMs;
            _timer.Start();
            return;
        }
        var speed = Math.Sqrt(vx * vx + vy * vy);
        if (speed < settings.MinimumSpeed || !double.IsFinite(speed)) return;
        var scale = Math.Min(speed, settings.MaximumSpeed) / speed * settings.Strength;
        _motions[id.Handle] = new Motion { Identity = id, X = rect.Left, Y = rect.Top,
            Vx = vx * scale, Vy = vy * scale,
            LaunchVx = vx * scale, LaunchVy = vy * scale,
            Stamp = Stopwatch.GetTimestamp() };
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
                var targetX = motion.SpringX == 0 ? motion.X + motion.Vx * step : motion.X;
                var targetY = motion.SpringY == 0 ? motion.Y + motion.Vy * step : motion.Y;
                var constrained = MonitorManager.Constrain(rect, targetX, targetY);
                if (motion.SpringX != 0)
                    AdvanceSpring(ref motion.X, ref motion.Vx, ref motion.SpringX, motion.BoundaryX,
                        motion.OvershootLimitX, dt);
                else if (constrained.BounceX)
                {
                    motion.X = motion.BoundaryX = constrained.X;
                    motion.SpringX = Math.Sign(targetX - constrained.X);
                    motion.OvershootLimitX = Math.Min(320, Math.Max(0, rect.Width - 40));
                    motion.Vx = ImpactVelocity(motion.Vx, motion.LaunchVx, motion.SpringX);
                }
                else { motion.X = constrained.X; motion.Vx *= decay; }

                if (motion.SpringY != 0)
                    AdvanceSpring(ref motion.Y, ref motion.Vy, ref motion.SpringY, motion.BoundaryY,
                        motion.OvershootLimitY, dt);
                else if (constrained.BounceY)
                {
                    motion.Y = motion.BoundaryY = constrained.Y;
                    motion.SpringY = Math.Sign(targetY - constrained.Y);
                    motion.OvershootLimitY = Math.Min(320, Math.Max(0, rect.Height - 40));
                    motion.Vy = ImpactVelocity(motion.Vy, motion.LaunchVy, motion.SpringY);
                }
                else { motion.Y = constrained.Y; motion.Vy *= decay; }
                if (!WindowMover.Move(motion.Identity, (int)Math.Round(motion.X), (int)Math.Round(motion.Y)) ||
                    (motion.SpringX == 0 && motion.SpringY == 0 &&
                     Math.Sqrt(motion.Vx * motion.Vx + motion.Vy * motion.Vy) < 20))
                    _motions.Remove(hwnd);
            }
            catch { _motions.Remove(hwnd); }
        }
        if (_motions.Count == 0) _timer.Stop();
    }

    private static double ImpactVelocity(double current, double launch, int direction)
    {
        // Friction can make a fast throw look like a slow impact by the time it reaches an edge.
        var impact = Math.Max(Math.Abs(current), Math.Abs(launch) * .45);
        return impact < 350 ? 0 : direction * Math.Clamp(impact * 3, 3000, 5200);
    }

    private static void AdvanceSpring(ref double position, ref double velocity, ref int direction,
        double boundary, double maximumOvershoot, double dt)
    {
        // A fast impact overshoots; a slow one eases straight into a small inset.
        const double inset = 18;
        const double stiffness = 45;
        const double damping = 10;
        var rest = boundary - direction * inset;
        velocity += (-stiffness * (position - rest) - damping * velocity) * dt;
        var next = position + velocity * dt;
        if (direction * (next - boundary) > maximumOvershoot)
        {
            next = boundary + direction * maximumOvershoot;
            velocity = 0;
        }
        position = next;
        if (Math.Abs(position - rest) < .75 && Math.Abs(velocity) < 12)
        {
            position = rest;
            velocity = 0;
            direction = 0;
        }
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
