using System.Globalization;
using System.Runtime.InteropServices;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Windows.Display;
using LiveWallpaper.Windows.Interop;
using LiveWallpaper.Windows.Services;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            ClockFormats();
            AttachmentLifecycle();
            FailedAttachmentRollsBack();
            DestroyedParentIsDetected();
            Console.WriteLine("PASS: clock formats, native attachment/geometry/styles, resize, rollback, cleanup, parent loss");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ClockFormats()
    {
        var clock = new ClockSettings();
        var time = new DateTime(2026, 9, 20, 18, 42, 37);
        Equal("18:42:37", time.ToString(clock.GetTimeFormat(), CultureInfo.InvariantCulture), "default HH:mm:ss");
        Equal("00:00:00", time.Date.ToString(clock.GetTimeFormat(), CultureInfo.InvariantCulture), "midnight");
        clock.ShowSeconds = false;
        Equal("18:42", time.ToString(clock.GetTimeFormat(), CultureInfo.InvariantCulture), "seconds off");
        clock.Use24HourClock = false;
        clock.ShowSeconds = true;
        Equal("06:42:37 PM", time.ToString(clock.GetTimeFormat(), CultureInfo.InvariantCulture), "12-hour");
    }

    private static void AttachmentLifecycle()
    {
        // Real HWNDs validate P/Invoke behavior without changing Explorer or
        // requiring an interactive desktop on the hosted Actions runner.
        var parent = Create(-320, -180);
        var child = Create(0, 0);
        var foreground = GetForegroundWindow();
        var style = NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlStyle);
        var exStyle = NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlExStyle);
        using var host = new WorkerWWallpaperHost();
        try
        {
            host.AttachToParent(child, parent, new DisplayBounds(0, 0, 640, 480));
            Check(host.IsAttached, "attached");
            Equal(parent, NativeMethods.GetParent(child), "parent");
            Check((NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlStyle).ToInt64() & NativeMethods.WsChild) != 0, "WS_CHILD");
            Check((NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlStyle).ToInt64() & NativeMethods.WsPopup) == 0, "no WS_POPUP");
            var actualEx = NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlExStyle).ToInt64();
            Check((actualEx & NativeMethods.WsExAppWindow) == 0, "no taskbar entry");
            Check((actualEx & NativeMethods.WsExNoActivate) != 0, "no activation");
            AssertRect(child, 0, 0, 640, 480);
            host.Resize(new DisplayBounds(40, 20, 800, 600));
            AssertRect(child, 40, 20, 800, 600);
            host.Attach(child, new DisplayBounds(0, 0, 640, 480)); // idempotent
            Equal(foreground, GetForegroundWindow(), "foreground preserved");
            host.Dispose();
            host.Dispose();
            Check(!host.IsAttached, "detached");
            Equal(nint.Zero, NativeMethods.GetParent(child), "original parent restored");
            Equal(style, NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlStyle), "style restored");
            Equal(exStyle, NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlExStyle), "ex style restored");
            Check(!IsWindowVisible(child), "detached window remains hidden");
            Check(NativeMethods.IsWindow(parent), "host window not destroyed");
        }
        finally
        {
            host.Dispose();
            DestroyWindow(child);
            DestroyWindow(parent);
        }
    }

    private static void FailedAttachmentRollsBack()
    {
        var parent = Create(0, 0);
        var child = Create(0, 0);
        var style = NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlStyle);
        using var host = new WorkerWWallpaperHost();
        try
        {
            try
            {
                host.AttachToParent(child, parent, new DisplayBounds(0, 0, 0, 100));
                throw new Exception("Expected invalid bounds failure.");
            }
            catch (ArgumentOutOfRangeException) { }
            Equal(nint.Zero, NativeMethods.GetParent(child), "rollback parent");
            Equal(style, NativeMethods.GetWindowLongPtr(child, NativeMethods.GwlStyle), "rollback style");
            Check(!host.IsAttached && !IsWindowVisible(child), "rollback hides window");
            // The same host is usable after rollback.
            host.AttachToParent(child, parent, new DisplayBounds(0, 0, 100, 100));
            Check(host.IsAttached, "retry after failure");
        }
        finally
        {
            host.Dispose();
            DestroyWindow(child);
            DestroyWindow(parent);
        }
    }

    private static void DestroyedParentIsDetected()
    {
        var parent = Create(0, 0);
        var child = Create(0, 0);
        using var host = new WorkerWWallpaperHost();
        host.AttachToParent(child, parent, new DisplayBounds(0, 0, 100, 100));
        DestroyWindow(parent);
        Check(!host.IsAttached, "Explorer-style parent loss detected");
        host.Dispose();
        Check(!NativeMethods.IsWindow(child), "no surviving child after parent destruction");
    }

    private static nint Create(int x, int y)
    {
        var hwnd = CreateWindowEx(0, "STATIC", "LiveWallpaper native test", 0x80000000,
            x, y, 1000, 800, 0, 0, 0, 0);
        Check(hwnd != 0, $"CreateWindowEx: {Marshal.GetLastPInvokeError()}");
        return hwnd;
    }

    private static void AssertRect(nint hwnd, int x, int y, int width, int height)
    {
        Check(GetWindowRect(hwnd, out var rect), "GetWindowRect");
        Equal(new DisplayBounds(x, y, width, height),
            new DisplayBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), "screen bounds");
    }

    private static void Check(bool success, string label)
    {
        if (!success) throw new Exception($"FAIL: {label}");
    }

    private static void Equal<T>(T expected, T actual, string label)
        => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{label}: expected {expected}, got {actual}");

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out NativeMethods.Rect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
