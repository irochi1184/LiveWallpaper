using Microsoft.UI.Xaml;

namespace LiveWallpaper.App;

public sealed partial class MainWindow : Window
{
    private WallpaperWindow? _wallpaperWindow;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        Title = "LiveWallpaper";
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(680, 460));
        Closed += MainWindow_Closed;
    }

    private void ShowWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wallpaperWindow != null)
            return;
        ShowWallpaperButton.IsEnabled = false;
        try
        {
            _wallpaperWindow = new WallpaperWindow();
            _wallpaperWindow.Closed += WallpaperWindow_Closed;
            _wallpaperWindow.DesktopConnectionLost += WallpaperWindow_DesktopConnectionLost;
            _wallpaperWindow.ShowOnDesktop();
            CloseWallpaperButton.IsEnabled = true;
            StatusText.Text = "プライマリ画面のデスクトップ背景に時計を表示しています。";
        }
        catch (Exception exception)
        {
            CloseWallpaper();
            StatusText.Text = $"時計を表示できませんでした。{exception.Message}";
        }
    }

    private void CloseWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWallpaper();
        StatusText.Text = "時計を閉じました。元の壁紙に戻りました。";
    }

    private void WallpaperWindow_DesktopConnectionLost(object? sender, string message)
    {
        CloseWallpaper();
        StatusText.Text = message;
    }

    private void WallpaperWindow_Closed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(sender, _wallpaperWindow))
        {
            _wallpaperWindow = null;
            if (!_closing)
            {
                ShowWallpaperButton.IsEnabled = true;
                CloseWallpaperButton.IsEnabled = false;
                StatusText.Text = "時計が閉じられました。再表示できます。";
            }
        }
    }

    private void CloseWallpaper()
    {
        var wallpaper = _wallpaperWindow;
        _wallpaperWindow = null;
        if (wallpaper != null)
        {
            wallpaper.Closed -= WallpaperWindow_Closed;
            wallpaper.DesktopConnectionLost -= WallpaperWindow_DesktopConnectionLost;
            wallpaper.StopAndClose();
        }
        if (!_closing)
        {
            ShowWallpaperButton.IsEnabled = true;
            CloseWallpaperButton.IsEnabled = false;
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _closing = true;
        CloseWallpaper();
    }
}
