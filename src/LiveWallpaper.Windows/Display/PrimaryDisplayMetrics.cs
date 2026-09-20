using LiveWallpaper.Windows.Interop;

namespace LiveWallpaper.Windows.Display;

public static class PrimaryDisplayMetrics
{
    public static int Width => NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);

    public static int Height => NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
}
