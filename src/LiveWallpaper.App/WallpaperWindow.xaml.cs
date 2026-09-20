using System.Globalization;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Windows.Display;
using LiveWallpaper.Windows.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace LiveWallpaper.App;

public sealed partial class WallpaperWindow : Window
{
    private readonly ClockSettings _clockSettings = new();
    private readonly WorkerWWallpaperHost _wallpaperHost = new();
    private readonly DispatcherQueueTimer _clockTimer;
    private readonly AppWindow _appWindow;
    private string _lastDisplayedTime = string.Empty;
    private DisplayBounds _bounds;
    private bool _closed;
    private int _ticks;

    public event EventHandler<string>? DesktopConnectionLost;

    public WallpaperWindow()
    {
        InitializeComponent();
        Title = "LiveWallpaper Clock";
        var windowHandle = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle));
        _appWindow.IsShownInSwitchers = false;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromMilliseconds(250);
        _clockTimer.Tick += ClockTimer_Tick;
        Closed += WallpaperWindow_Closed;
        ClockText.FontSize = _clockSettings.FontSize;
        ClockText.Opacity = _clockSettings.Opacity;
        ClockText.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(_clockSettings.FontFamily);
        UpdateClock();
    }

    public void ShowOnDesktop()
    {
        _bounds = PrimaryDisplayMetrics.GetBounds();
        _wallpaperHost.Attach(WindowNative.GetWindowHandle(this), _bounds);
        // Initialize WinUI rendering without activating the wallpaper.
        _appWindow.Show(false);
        _wallpaperHost.Resize(_bounds);
        UpdateClock();
        _clockTimer.Start();
    }

    public void StopAndClose()
    {
        if (_closed)
            return;
        _clockTimer.Stop();
        _wallpaperHost.Dispose();
        Close();
    }

    private void ClockTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        UpdateClock();
        if (++_ticks % 4 != 0)
            return;
        try
        {
            if (!_wallpaperHost.IsAttached)
                throw new InvalidOperationException("デスクトップとの接続が失われました。時計を再表示してください。");
            var bounds = PrimaryDisplayMetrics.GetBounds();
            // Reapply position once a second: the parent's origin can change
            // even when the primary monitor dimensions stay the same.
            _wallpaperHost.Resize(bounds);
            _bounds = bounds;
        }
        catch (Exception exception)
        {
            _clockTimer.Stop();
            DesktopConnectionLost?.Invoke(this, exception.Message);
        }
    }

    private void UpdateClock()
    {
        // Read OS time, never accumulate timer ticks. InvariantCulture keeps
        // literal HH:mm:ss separators even under a different Windows locale.
        var text = DateTime.Now.ToString(_clockSettings.GetTimeFormat(), CultureInfo.InvariantCulture);
        if (text == _lastDisplayedTime)
            return;
        _lastDisplayedTime = text;
        ClockText.Text = text;
    }

    private void WallpaperWindow_Closed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _clockTimer.Stop();
        _clockTimer.Tick -= ClockTimer_Tick;
        _wallpaperHost.Dispose();
    }
}
