using System.Text;

namespace ToolInertia;

internal readonly record struct WindowIdentity(nint Handle, uint ProcessId, uint ThreadId, string ClassName);

internal static class WindowMover
{
    internal static WindowIdentity? Identify(nint hwnd)
    {
        if (hwnd == 0 || !Native.IsWindow(hwnd)) return null;
        var tid = Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (tid == 0 || pid == 0) return null;
        var name = new StringBuilder(256);
        Native.GetClassName(hwnd, name, name.Capacity);
        return new(hwnd, pid, tid, name.ToString());
    }

    internal static bool Same(WindowIdentity id)
    {
        var now = Identify(id.Handle);
        return now == id;
    }

    internal static bool Eligible(nint hwnd)
    {
        if (!Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd) || Native.IsZoomed(hwnd)) return false;
        if (Native.GetAncestor(hwnd, Native.GaRoot) != hwnd) return false;
        var style = (long)Native.GetWindowLongPtr(hwnd, Native.GwStyle);
        var exStyle = (long)Native.GetWindowLongPtr(hwnd, Native.GwExStyle);
        if ((style & Native.WsCaption) != Native.WsCaption || (exStyle & Native.WsExToolWindow) != 0) return false;
        var name = new StringBuilder(256);
        Native.GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32768") return false;
        if (Native.DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        if (!Native.GetWindowRect(hwnd, out var rect) || rect.Width < 100 || rect.Height < 30) return false;
        var monitor = Native.MonitorFromRect(ref rect, Native.MonitorDefaultToNearest);
        var info = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        if (monitor == 0 || !Native.GetMonitorInfo(monitor, ref info)) return false;
        // Occupying the complete display is likely fullscreen even if WS_CAPTION remains set.
        if (rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top &&
            rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom) return false;
        return true;
    }

    internal static bool Move(WindowIdentity id, int x, int y)
    {
        if (!Same(id) || !Eligible(id.Handle)) return false;
        return Native.SetWindowPos(id.Handle, 0, x, y, 0, 0,
            Native.SwpNoSize | Native.SwpNoZOrder | Native.SwpNoActivate | Native.SwpNoOwnerZOrder);
    }
}
