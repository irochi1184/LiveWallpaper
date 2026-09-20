using System.ComponentModel;
using System.Runtime.InteropServices;
using LiveWallpaper.Windows.Interop;

namespace LiveWallpaper.Windows.Display;

public readonly record struct DisplayBounds(int X, int Y, int Width, int Height);

public static class PrimaryDisplayMetrics
{
    public static DisplayBounds GetBounds()
    {
        // The primary monitor contains (0, 0). Include the taskbar area.
        var monitor = NativeMethods.MonitorFromPoint(default, 1);
        var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "画面サイズを取得できませんでした。");
        return new(info.Monitor.Left, info.Monitor.Top,
            info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);
    }
}
