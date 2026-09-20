using System.ComponentModel;
using System.Runtime.InteropServices;
using LiveWallpaper.Windows.Display;
using LiveWallpaper.Windows.Interop;

namespace LiveWallpaper.Windows.Services;

/// <summary>Owns only our attachment; never closes or hides Explorer windows.</summary>
public sealed class WorkerWWallpaperHost : IDisposable
{
    private nint _window, _parent, _originalParent, _iconView;
    private long _style, _extendedStyle;

    public bool IsAttached => _window != 0 && NativeMethods.IsWindow(_window)
        && NativeMethods.IsWindow(_parent) && NativeMethods.GetParent(_window) == _parent;

    public void Attach(nint window, DisplayBounds bounds)
    {
        if (IsAttached && window == _window)
        {
            Resize(bounds);
            return;
        }
        var (parent, iconView) = FindDesktopTarget();
        if (parent == 0)
            throw new InvalidOperationException("デスクトップの壁紙領域（WorkerW）が見つかりません。Explorerの起動後に再試行してください。");
        AttachToParent(window, parent, bounds, iconView);
    }

    internal void AttachToParent(nint window, nint parent, DisplayBounds bounds, nint iconView = default)
    {
        if (_window != 0)
            throw new InvalidOperationException("壁紙は既に配置されています。");
        if (!NativeMethods.IsWindow(window) || !NativeMethods.IsWindow(parent))
            throw new ArgumentException("配置先または時計ウィンドウが無効です。");

        _window = window;
        _parent = parent;
        _iconView = iconView;
        _originalParent = NativeMethods.GetParent(window);
        _style = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlStyle).ToInt64();
        _extendedStyle = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle).ToInt64();
        try
        {
            NativeMethods.ShowWindow(window, 0);
            SetStyle(NativeMethods.GwlStyle,
                (_style & ~(NativeMethods.WsPopup | NativeMethods.WsCaption | NativeMethods.WsThickFrame)) | NativeMethods.WsChild);
            SetStyle(NativeMethods.GwlExStyle,
                (_extendedStyle & ~NativeMethods.WsExAppWindow) | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate);
            Marshal.SetLastPInvokeError(0);
            var previous = NativeMethods.SetParent(window, parent);
            var error = Marshal.GetLastPInvokeError();
            // NULL is also a valid previous parent for top-level windows.
            if (previous == 0 && error != 0)
                throw new Win32Exception(error, "デスクトップへの配置に失敗しました。");
            if (!IsAttached)
                throw new InvalidOperationException("配置中にデスクトップの構成が変わりました。再試行してください。");
            Resize(bounds);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Resize(DisplayBounds bounds)
    {
        if (!IsAttached)
            throw new InvalidOperationException("デスクトップとの接続が失われました。時計を再表示してください。");
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));

        // Child positions are relative to the parent client area. WorkerW can
        // start at a negative virtual-screen origin on multi-monitor systems.
        var point = new NativeMethods.Point { X = bounds.X, Y = bounds.Y };
        Marshal.SetLastPInvokeError(0);
        var mapped = NativeMethods.MapWindowPoints(0, _parent, ref point, 1);
        var error = Marshal.GetLastPInvokeError();
        if (mapped == 0 && error != 0)
            throw new Win32Exception(error, "壁紙の座標変換に失敗しました。");
        if (!NativeMethods.SetWindowPos(_window, _iconView /* below icons, or HWND_TOP within classic WorkerW */, point.X, point.Y,
            bounds.Width, bounds.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged | NativeMethods.SwpShowWindow))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "壁紙の位置とサイズを設定できませんでした。");
    }

    private void SetStyle(int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = NativeMethods.SetWindowLongPtr(_window, index, new nint(value));
        var error = Marshal.GetLastPInvokeError();
        if (previous == 0 && error != 0)
            throw new Win32Exception(error, "壁紙ウィンドウの属性を設定できませんでした。");
    }

    public void Dispose()
    {
        if (_window != 0 && NativeMethods.IsWindow(_window))
        {
            NativeMethods.ShowWindow(_window, 0);
            // Restore while hidden before WinUI closes its top-level window.
            // Explorer may already have destroyed the child during a restart.
            NativeMethods.SetParent(_window, NativeMethods.IsWindow(_originalParent) ? _originalParent : 0);
            NativeMethods.SetWindowLongPtr(_window, NativeMethods.GwlStyle, new nint(_style));
            NativeMethods.SetWindowLongPtr(_window, NativeMethods.GwlExStyle, new nint(_extendedStyle));
        }
        _window = _parent = _originalParent = _iconView = 0;
    }

    private static (nint Parent, nint IconView) FindDesktopTarget()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == 0)
            return default;
        SpawnWorkerW(progman, 0xD, 1);
        // Raised desktop: attach beside the wallpaper WorkerW, below icons.
        var icons = NativeMethods.FindWindowEx(progman, 0, "SHELLDLL_DefView", null);
        if (icons != 0 && (NativeMethods.GetWindowLongPtr(progman, NativeMethods.GwlExStyle).ToInt64() & 0x00200000L) != 0)
            return (progman, icons);
        var worker = EnumerateWorkerW(progman);
        if (worker != 0)
            return (worker, 0);
        SpawnWorkerW(progman, 0, 0);
        SpawnWorkerW(progman, 0xD, 0);
        SpawnWorkerW(progman, 0xD, 1);
        return (EnumerateWorkerW(progman), 0);
    }

    private static void SpawnWorkerW(nint progman, nint wParam, nint lParam)
        => NativeMethods.SendMessageTimeout(progman, 0x052C, wParam, lParam,
            0x0002 /* SMTO_ABORTIFHUNG */, 1000, out _);

    private static nint EnumerateWorkerW(nint progman)
    {
        nint worker = 0;
        NativeMethods.EnumWindows((topLevel, _) =>
        {
            if (NativeMethods.FindWindowEx(topLevel, 0, "SHELLDLL_DefView", null) == 0)
                return true;
            // Classic layout: the next WorkerW is behind the desktop icon host.
            var candidate = NativeMethods.FindWindowEx(0, topLevel, "WorkerW", null);
            if (candidate != 0 && NativeMethods.FindWindowEx(candidate, 0, "SHELLDLL_DefView", null) == 0)
                worker = candidate;
            return worker == 0;
        }, 0);
        if (worker != 0)
            return worker;
        // New Explorer layout: WorkerW and the icon view are siblings in Progman.
        if (NativeMethods.FindWindowEx(progman, 0, "SHELLDLL_DefView", null) != 0)
        {
            var candidate = NativeMethods.FindWindowEx(progman, 0, "WorkerW", null);
            if (candidate != 0 && NativeMethods.FindWindowEx(candidate, 0, "SHELLDLL_DefView", null) == 0)
                return candidate;
        }
        return 0;
    }
}
