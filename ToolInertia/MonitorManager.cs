namespace ToolInertia;

internal static class MonitorManager
{
    internal static (double X, double Y, bool BounceX, bool BounceY) Constrain(Native.Rect rect, double x, double y)
    {
        var displays = new List<Native.Rect>();
        Native.MonitorProc callback = (nint monitor, nint hdc, ref Native.Rect monitorRect, nint data) =>
        {
            var info = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
            if (Native.GetMonitorInfo(monitor, ref info)) displays.Add(info.Monitor);
            return true;
        };
        Native.EnumDisplayMonitors(0, 0, callback, 0);
        if (displays.Count == 0) return (rect.Left, rect.Top, true, true);

        var startX = (double)rect.Left;
        var startY = (double)rect.Top;
        if (!Fits(startX, startY))
        {
            var nearest = displays.Select(d => (
                X: (double)Math.Clamp(rect.Left, d.Left, Math.Max(d.Left, d.Right - rect.Width)),
                Y: (double)Math.Clamp(rect.Top, d.Top, Math.Max(d.Top, d.Bottom - rect.Height))))
                .OrderBy(p => Math.Abs(p.X - startX) + Math.Abs(p.Y - startY)).First();
            startX = nearest.X;
            startY = nearest.Y;
        }

        var boundedX = Limit(startX, x, candidate => Fits(candidate, startY));
        var boundedY = Limit(startY, y, candidate => Fits(boundedX, candidate));
        return (boundedX, boundedY, Math.Abs(boundedX - x) > .01, Math.Abs(boundedY - y) > .01);

        double Limit(double from, double to, Func<double, bool> fits)
        {
            if (fits(to)) return to;
            if (!fits(from)) return from;
            double low = 0, high = 1;
            for (var i = 0; i < 24; i++)
            {
                var mid = (low + high) / 2;
                if (fits(from + (to - from) * mid)) low = mid;
                else high = mid;
            }
            return from + (to - from) * low;
        }

        bool Fits(double left, double top)
        {
            var right = left + rect.Width;
            var bottom = top + rect.Height;
            var edges = displays.SelectMany(d => new[] { Math.Max(left, d.Left), Math.Min(right, d.Right) })
                .Where(edge => edge >= left && edge <= right).Append(left).Append(right).Distinct().Order().ToArray();
            for (var i = 0; i < edges.Length - 1; i++)
            {
                if (edges[i + 1] <= edges[i]) continue;
                var middle = (edges[i] + edges[i + 1]) / 2;
                var coveredTo = top;
                foreach (var display in displays.Where(d => d.Left <= middle && d.Right >= middle && d.Bottom > top && d.Top < bottom)
                    .OrderBy(d => d.Top))
                {
                    if (display.Top > coveredTo) break;
                    coveredTo = Math.Max(coveredTo, display.Bottom);
                    if (coveredTo >= bottom) break;
                }
                if (coveredTo < bottom) return false;
            }
            return true;
        }
    }
}
