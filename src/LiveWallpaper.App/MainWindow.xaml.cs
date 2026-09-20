using Microsoft.UI.Xaml;

namespace LiveWallpaper.App;

public sealed partial class MainWindow : Window
{
    private WallpaperWindow? _wallpaperWindow;

    public MainWindow()
    {
        InitializeComponent();
        Title = "LiveWallpaper";
    }

    private void ShowWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        _wallpaperWindow ??= new WallpaperWindow();
        _wallpaperWindow.Activate();

        StatusText.Text = _wallpaperWindow.TryAttachToDesktop()
            ? "デスクトップ背景へ時計ウィンドウを配置しました。"
            : "WorkerWを取得できませんでした。Explorerの状態やWindows更新の影響を確認してください。";
    }

    private void CloseWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        _wallpaperWindow?.Close();
        _wallpaperWindow = null;
        StatusText.Text = "時計ウィンドウを閉じました。";
    }
}
