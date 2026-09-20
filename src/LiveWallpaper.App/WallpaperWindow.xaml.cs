using LiveWallpaper.Core.Models;
using LiveWallpaper.Windows.Display;
using LiveWallpaper.Windows.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace LiveWallpaper.App;

public sealed partial class WallpaperWindow : Window
{
    private readonly ClockSettings _clockSettings = new();
    private readonly WorkerWWallpaperHost _wallpaperHost = new();
    private readonly DispatcherQueueTimer _clockTimer;
    private readonly AppWindow _appWindow;
    private string _lastDisplayedTime = string.Empty;

    public WallpaperWindow()
    {
        InitializeComponent();

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        ConfigureWallpaperWindow();

        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromMilliseconds(250);
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();

        Closed += WallpaperWindow_Closed;

        ApplyClockSettings();
        UpdateClock();
    }

    public bool TryAttachToDesktop()
    {
        SizeToPrimaryDisplay();

        var windowHandle = WindowNative.GetWindowHandle(this);
        return _wallpaperHost.TryAttach(windowHandle);
    }

    private void ConfigureWallpaperWindow()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
    }

    private void SizeToPrimaryDisplay()
    {
        _appWindow.MoveAndResize(new RectInt32(
            0,
            0,
            PrimaryDisplayMetrics.Width,
            PrimaryDisplayMetrics.Height));
    }

    private void ApplyClockSettings()
    {
        ClockText.FontSize = _clockSettings.FontSize;
        ClockText.Opacity = _clockSettings.Opacity;
        ClockText.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(_clockSettings.FontFamily);
    }

    private void ClockTimer_Tick(DispatcherQueueTimer sender, object args)
        => UpdateClock();

    private void UpdateClock()
    {
        // Timerの回数を時刻として数えず、毎回OSの現在時刻を取得する。
        // これにより処理遅延が起きても時計が徐々にずれていかない。
        var text = DateTime.Now.ToString(_clockSettings.GetTimeFormat());

        if (text == _lastDisplayedTime)
        {
            return;
        }

        _lastDisplayedTime = text;
        ClockText.Text = text;
    }

    private void WallpaperWindow_Closed(object sender, WindowEventArgs args)
    {
        _clockTimer.Stop();
    }
}
