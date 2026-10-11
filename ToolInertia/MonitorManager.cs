namespace ToolInertia;

internal static class MonitorManager
{
    internal static (double X, double Y, bool StopX, bool StopY) Constrain(Native.Rect rect, double x, double y)
    {
        var workAreas = new List<Native.Rect>();
        Native.MonitorProc callback = (nint monitor, nint hdc, ref Native.Rect monitorRect, nint data) =>
        {
            var info = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
            if (Native.GetMonitorInfo(monitor, ref info)) workAreas.Add(info.Work);
            return true;
        };
        Native.EnumDisplayMonitors(0, 0, callback, 0);
        if (workAreas.Count == 0) return (rect.Left, rect.Top, true, true);
        // Choose the work area requiring the least correction. This permits crossing
        // directly between adjacent displays while keeping a grab area visible.
        var best = workAreas.Select(work =>
        {
            var cx = Math.Clamp(x, work.Left - rect.Width + 100, work.Right - 100);
            var cy = Math.Clamp(y, work.Top, work.Bottom - 32);
            var cost = Math.Abs(cx - x) + Math.Abs(cy - y);
            return (X: cx, Y: cy, Cost: cost);
        }).OrderBy(item => item.Cost).First();
        return (best.X, best.Y, best.X != x, best.Y != y);
    }
}
