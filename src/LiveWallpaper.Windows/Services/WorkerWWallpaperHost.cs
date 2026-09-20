using System.Runtime.InteropServices;
using LiveWallpaper.Windows.Interop;

namespace LiveWallpaper.Windows.Services;

/// <summary>
/// Windows Explorerが生成するWorkerWへ描画用ウィンドウを配置する技術検証。
/// WorkerWは公開APIではないため、このクラス以外へ依存を広げない。
/// </summary>
public sealed class WorkerWWallpaperHost
{
    public bool TryAttach(nint wallpaperWindowHandle)
    {
        if (wallpaperWindowHandle == nint.Zero)
        {
            return false;
        }

        var workerW = FindWorkerW();
        if (workerW == nint.Zero)
        {
            return false;
        }

        Marshal.SetLastPInvokeError(0);
        _ = NativeMethods.SetParent(wallpaperWindowHandle, workerW);

        return Marshal.GetLastPInvokeError() == 0;
    }

    private static nint FindWorkerW()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == nint.Zero)
        {
            return nint.Zero;
        }

        SpawnWorkerW(progman, nint.Zero, nint.Zero);

        var workerW = EnumerateWorkerW();
        if (workerW != nint.Zero)
        {
            return workerW;
        }

        // Windowsの更新でExplorer側の生成方法が変わる場合に備えた代替呼び出し。
        SpawnWorkerW(progman, new nint(0xD), new nint(0x1));
        return EnumerateWorkerW();
    }

    private static void SpawnWorkerW(nint progman, nint wParam, nint lParam)
    {
        _ = NativeMethods.SendMessageTimeout(
            progman,
            NativeMethods.SpawnWorkerWMessage,
            wParam,
            lParam,
            NativeMethods.SmtoNormal,
            1000,
            out _);
    }

    private static nint EnumerateWorkerW()
    {
        nint workerW = nint.Zero;

        _ = NativeMethods.EnumWindows((topLevelWindow, _) =>
        {
            var shellView = NativeMethods.FindWindowEx(
                topLevelWindow,
                nint.Zero,
                "SHELLDLL_DefView",
                null);

            if (shellView == nint.Zero)
            {
                return true;
            }

            workerW = NativeMethods.FindWindowEx(
                nint.Zero,
                topLevelWindow,
                "WorkerW",
                null);

            return workerW == nint.Zero;
        }, nint.Zero);

        return workerW;
    }
}
