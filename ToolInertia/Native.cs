using System.Runtime.InteropServices;
using System.Text;

namespace ToolInertia;

internal static class Native
{
    internal const uint MoveStart = 0x000A, MoveEnd = 0x000B;
    internal const uint ObjectDestroy = 0x8001;
    internal const uint OutOfContext = 0, SkipOwnProcess = 2;
    internal const int ObjIdWindow = 0, WhMouseLl = 14;
    internal const int WmLeftDown = 0x0201, WmLeftUp = 0x0202;
    internal const int SwpNoSize = 1, SwpNoZOrder = 4, SwpNoActivate = 0x10, SwpNoOwnerZOrder = 0x200;
    internal const int GwStyle = -16, GwExStyle = -20, WsCaption = 0x00C00000;
    internal const int WsExToolWindow = 0x80, MonitorDefaultToNearest = 2;
    internal const int GaRoot = 2, VkLButton = 1;

    [StructLayout(LayoutKind.Sequential)] internal struct Rect
    {
        internal int Left, Top, Right, Bottom;
        internal readonly int Width => Right - Left;
        internal readonly int Height => Bottom - Top;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorInfo
    {
        internal int Size;
        internal Rect Monitor, Work;
        internal int Flags;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseData
    {
        internal Point Pt;
        internal uint Data, Flags, Time;
        internal nuint ExtraInfo;
    }
    internal delegate void WinEventProc(nint hook, uint evt, nint hwnd, int objectId, int childId, uint threadId, uint time);
    internal delegate nint MouseProc(int code, nint message, nint data);
    internal delegate bool MonitorProc(nint monitor, nint hdc, ref Rect rect, nint data);

    [DllImport("user32.dll")] internal static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int id, MouseProc callback, nint module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, int flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd, int flags);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] internal static extern nint MonitorFromRect(ref Rect rect, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorProc callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
}
