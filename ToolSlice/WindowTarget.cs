using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace ToolSlice;

public sealed record WindowTarget(IntPtr Handle, string Title, Rectangle Bounds, RectangleF Crop)
{
    public static WindowTarget Whole(IntPtr hwnd)
    {
        if (!WindowCatalog.TryGetWindow(hwnd, out var title, out var bounds)) throw new InvalidOperationException("원본 창을 찾을 수 없습니다.");
        return new WindowTarget(hwnd, title, bounds, new RectangleF(0, 0, 1, 1));
    }

    public static WindowTarget Region(IntPtr hwnd, Rectangle selected)
    {
        var whole = Whole(hwnd);
        var clipped = Rectangle.Intersect(whole.Bounds, selected);
        if (clipped.Width < 8 || clipped.Height < 8) throw new InvalidOperationException("선택한 영역이 창 안에 충분히 포함되지 않습니다.");
        var b = whole.Bounds;
        return whole with { Crop = new RectangleF(
            (float)(clipped.Left - b.Left) / b.Width, (float)(clipped.Top - b.Top) / b.Height,
            (float)clipped.Width / b.Width, (float)clipped.Height / b.Height) };
    }

}

public static class WindowCatalog
{
    public static IReadOnlyList<WindowTarget> List()
    {
        var windows = new List<WindowTarget>();
        EnumWindows((hwnd, _) =>
        {
            if (TryGetWindow(hwnd, out var title, out var bounds))
                windows.Add(new WindowTarget(hwnd, title, bounds, new RectangleF(0, 0, 1, 1)));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static IntPtr WindowAt(Point point)
    {
        var hwnd = WindowFromPoint(point);
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        hwnd = GetAncestor(hwnd, 2);
        return TryGetWindow(hwnd, out _, out _) ? hwnd : IntPtr.Zero;
    }

    public static bool TryGetWindow(IntPtr hwnd, out string title, out Rectangle bounds)
    {
        title = "";
        bounds = Rectangle.Empty;
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return false;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == (uint)Environment.ProcessId) return false;
        var length = GetWindowTextLength(hwnd);
        if (length == 0) return false;
        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        title = sb.ToString();
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (DwmGetWindowAttribute(hwnd, 9, out var rect, Marshal.SizeOf<Rect>()) != 0 && !GetWindowRect(hwnd, out rect)) return false;
        bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return bounds.Width > 30 && bounds.Height > 30;
    }

    public static bool IsAlive(IntPtr hwnd) => IsWindow(hwnd);
    public static bool IsMinimized(IntPtr hwnd) => IsWindow(hwnd) && IsIconic(hwnd);
    public static bool IsHidden(IntPtr hwnd) => IsWindow(hwnd) && !IsWindowVisible(hwnd);
    public static void Restore(IntPtr hwnd)
    {
        if (IsWindow(hwnd)) ShowWindowAsync(hwnd, IsIconic(hwnd) ? 9 : 5);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out Rect value, int size);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}
