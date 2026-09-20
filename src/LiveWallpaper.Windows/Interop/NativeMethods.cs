using System.Runtime.InteropServices;

namespace LiveWallpaper.Windows.Interop;

internal static class NativeMethods
{
    internal const int GwlStyle = -16, GwlExStyle = -20;
    internal const long WsChild = 0x40000000L, WsPopup = 0x80000000L;
    internal const long WsCaption = 0x00C00000L, WsThickFrame = 0x00040000L;
    internal const long WsExAppWindow = 0x00040000L, WsExToolWindow = 0x80L, WsExNoActivate = 0x08000000L;
    internal const uint SwpNoActivate = 0x10, SwpFrameChanged = 0x20, SwpShowWindow = 0x40;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal Rect Monitor, Work;
        internal uint Flags;
    }

    internal delegate bool EnumWindowsProc(nint hwnd, nint parameter);

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindow(string? className, string? windowName);
    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindowEx(nint parent, nint after, string? className, string? windowName);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll")]
    internal static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static extern nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam,
        uint flags, uint timeout, out nint result);

    // Only 64-bit architectures (x64/ARM64) are supported.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int MapWindowPoints(nint from, nint to, ref Point point, uint count);
    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
