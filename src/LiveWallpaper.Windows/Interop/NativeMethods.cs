using System.Runtime.InteropServices;

namespace LiveWallpaper.Windows.Interop;

internal static partial class NativeMethods
{
    internal const uint SpawnWorkerWMessage = 0x052C;
    internal const uint SmtoNormal = 0x0000;

    internal const int SmCxScreen = 0;
    internal const int SmCyScreen = 1;

    internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindow(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindowEx(
        nint hWndParent,
        nint hWndChildAfter,
        string? lpszClass,
        string? lpszWindow);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetParent(nint hWndChild, nint hWndNewParent);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static partial nint SendMessageTimeout(
        nint hWnd,
        uint msg,
        nint wParam,
        nint lParam,
        uint fuFlags,
        uint uTimeout,
        out nint lpdwResult);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int nIndex);
}
