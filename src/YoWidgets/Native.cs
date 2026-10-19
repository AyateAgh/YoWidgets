using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace YoWidgets;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? cls, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? name);
    public delegate bool EnumProc(IntPtr hwnd, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr p);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetStyle(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] public static extern IntPtr SetStyle(IntPtr h, int index, IntPtr v);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
    [DllImport("user32.dll")] public static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    public static IntPtr Handle(Window w) => new WindowInteropHelper(w).Handle;
    public static RectD Bounds(Window w)
    {
        GetWindowRect(Handle(w), out var r);
        return new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
    public static void ToolWindow(Window w)
    {
        var h = Handle(w);
        var ex = GetStyle(h, -20).ToInt64();
        SetStyle(h, -20, new((ex | 0x80) & ~0x40000));
    }
}
