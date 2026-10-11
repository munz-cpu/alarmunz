using System.Runtime.InteropServices;

namespace ToolInertia;

internal sealed class MouseHook : IDisposable
{
    private readonly Native.MouseProc _callback;
    private nint _hook;
    internal event Action? Released;
    internal event Action<Native.Point>? Pressed;

    internal MouseHook()
    {
        _callback = OnMouse;
        _hook = Native.SetWindowsHookEx(Native.WhMouseLl, _callback, 0, 0);
        if (_hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private nint OnMouse(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            try
            {
                if (message == Native.WmLeftUp) Released?.Invoke();
                else if (message == Native.WmLeftDown)
                    Pressed?.Invoke(Marshal.PtrToStructure<Native.MouseData>(data).Pt);
            }
            catch { /* Never allow a managed exception to cross a native hook. */ }
        }
        return Native.CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_hook != 0) Native.UnhookWindowsHookEx(_hook);
        _hook = 0;
    }
}
