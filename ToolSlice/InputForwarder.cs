using System.Drawing;
using System.Runtime.InteropServices;

namespace ToolSlice;

// WGC has no input channel. This sends a user gesture to the original window with
// ordinary Win32 input, after activating it. Elevated and protected apps can reject it.
public sealed class InputForwarder(IntPtr hwnd)
{
    private Point? _start;
    private Point? _last;
    private bool _right;

    public void Down(Point source, bool right = false)
    {
        _start = _last = source;
        _right = right;
    }

    public void Move(Point source)
    {
        if (_start is not null) _last = source;
    }

    public bool Up(Point source, bool right = false)
    {
        if (_start is not Point start || _right != right) return false;
        _start = null;
        _last = null;
        return Replay(start, source, right);
    }

    public bool Wheel(Point source, int delta)
    {
        if (!WindowCatalog.IsAlive(hwnd) || !GetCursorPos(out var original)) return false;
        try
        {
            SetForegroundWindow(hwnd);
            if (!SetCursorPos(source.X, source.Y)) return false;
            Thread.Sleep(15);
            if (!HitIsSource(source)) return false;
            var sent = SendMouse(0x0800, delta);
            if (sent) Thread.Sleep(35);
            return sent;
        }
        finally { SetCursorPos(original.X, original.Y); }
    }

    private bool Replay(Point start, Point end, bool right)
    {
        if (!WindowCatalog.IsAlive(hwnd) || !GetCursorPos(out var original)) return false;
        var down = right ? 0x0008u : 0x0002u;
        var up = right ? 0x0010u : 0x0004u;
        var pressed = false;
        try
        {
            SetForegroundWindow(hwnd);
            if (!SetCursorPos(start.X, start.Y)) return false;
            Thread.Sleep(15);
            if (!HitIsSource(start)) return false;
            if (!SendMouse(down)) return false;
            pressed = true;
            Thread.Sleep(20);
            var distance = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
            if (distance > 4)
            {
                var steps = Math.Clamp(distance / 15, 4, 16);
                for (var i = 1; i <= steps; i++)
                {
                    var x = start.X + (end.X - start.X) * i / steps;
                    var y = start.Y + (end.Y - start.Y) * i / steps;
                    if (!SetCursorPos(x, y)) break;
                    Thread.Sleep(8);
                }
            }
            else Thread.Sleep(25);
            var released = SendMouse(up);
            pressed = !released;
            if (released) Thread.Sleep(35);
            return released;
        }
        finally { if (pressed) SendMouse(up); SetCursorPos(original.X, original.Y); }
    }

    private bool HitIsSource(Point screen)
    {
        var hit = WindowFromPoint(screen);
        return hit != IntPtr.Zero && GetAncestor(hit, 2) == hwnd;
    }

    private static bool SendMouse(uint flags, int data = 0)
    {
        var input = new Input { Type = 0, Mouse = new MouseInput { MouseData = (uint)data, Flags = flags } };
        return SendInput(1, [input], Marshal.SizeOf<Input>()) == 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public MouseInput Mouse; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx, Dy;
        public uint MouseData, Flags, Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
}
