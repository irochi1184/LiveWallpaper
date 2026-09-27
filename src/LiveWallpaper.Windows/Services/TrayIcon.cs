using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LiveWallpaper.Windows.Services;

/// <summary>Owns a notification icon on the window's UI thread. Dispose before destroying the HWND.</summary>
public sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8000 + 73;
    private readonly nint _window;
    private readonly SubclassProc _callback;
    private readonly uint _taskbarCreated;
    private NotifyIconData _data;
    private bool _disposed;
    public bool IsAvailable { get; private set; }
    public event Action? ShowRequested;
    public event Action? ExitRequested;
    public event Action? Unavailable;
    public event Action? SessionEnding;

    public TrayIcon(nint window)
    {
        _window = window;
        _callback = WindowProc;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _data = new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = window, Id = 1,
            Flags = 1 | 2 | 4 | 0x80, CallbackMessage = CallbackMessage,
            Icon = LoadIcon(0, (nint)32512), Tip = "LiveWallpaper — 設定を開く",
            Info = "", InfoTitle = ""
        };
        if (_taskbarCreated == 0 || !SetWindowSubclass(window, _callback, 1, 0))
            throw new Win32Exception("Could not receive notification icon messages.");
        if (!AddIcon())
        {
            Dispose();
            throw new Win32Exception("Could not create the notification icon.");
        }
    }

    private bool AddIcon()
    {
        IsAvailable = ShellNotifyIcon(0, ref _data);
        if (!IsAvailable) return false;
        _data.Version = 4;
        if (ShellNotifyIcon(4, ref _data)) return true;
        ShellNotifyIcon(2, ref _data);
        return IsAvailable = false;
    }

    private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        // Never let managed exceptions cross a native callback. The owner queues
        // UI work so closing/disposal cannot occur inside this subclass callback.
        try
        {
            if (!_disposed && message == _taskbarCreated)
            {
                if (!AddIcon()) Unavailable?.Invoke();
            }
            else if (!_disposed && message == CallbackMessage)
            {
                var notification = (uint)((long)lParam & 0xffff);
                if (notification is 0x400 or 0x401) ShowRequested?.Invoke(); // NIN_SELECT / NIN_KEYSELECT
                else if (notification == 0x7b) ShowMenu(wParam); // WM_CONTEXTMENU
                return 0;
            }
            else if (message == 0x16 && wParam != 0) SessionEnding?.Invoke(); // WM_ENDSESSION
        }
        catch
        {
            IsAvailable = false;
            try { Unavailable?.Invoke(); } catch { }
        }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void ShowMenu(nuint coordinates)
    {
        var point = new Point { X = (short)(coordinates & 0xffff), Y = (short)((coordinates >> 16) & 0xffff) };
        if (point.X == -1 && point.Y == -1) GetCursorPos(out point);
        var menu = CreatePopupMenu();
        if (menu == 0) return;
        uint command;
        try
        {
            AppendMenu(menu, 0, 1, "設定を開く");
            AppendMenu(menu, 0x800, 0, null);
            AppendMenu(menu, 0, 2, "LiveWallpaperを終了");
            SetForegroundWindow(_window);
            command = TrackPopupMenuEx(menu, 0x100 | 0x80 | 2, point.X, point.Y, _window, 0);
            PostMessage(_window, 0, 0, 0);
        }
        finally { DestroyMenu(menu); }
        if (command == 1) ShowRequested?.Invoke();
        else if (command == 2) ExitRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ShellNotifyIcon(2, ref _data);
        IsAvailable = false;
        RemoveWindowSubclass(_window, _callback, 1);
        // LoadIcon returned a shared system icon; it must not be destroyed.
    }

    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id, Flags, CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint action, ref NotifyIconData data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint msg, nuint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", EntryPoint = "LoadIconW")] private static extern nint LoadIcon(nint instance, nint name);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(nint hwnd, uint msg, nuint wParam, nint lParam);
}
