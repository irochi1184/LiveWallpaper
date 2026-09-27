using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.Windows.AppLifecycle;
using System.Runtime.InteropServices;

namespace LiveWallpaper.App;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    private AppInstance? _instance;
    private DispatcherQueue? _dispatcher;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Register before constructing MainWindow (and before reading/writing
        // settings). A second process only forwards activation, then exits.
        try
        {
            _instance = AppInstance.FindOrRegisterForKey("LiveWallpaper.Main");
            if (!_instance.IsCurrent)
            {
                await _instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
                Exit();
                return;
            }
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _instance.Activated += Instance_Activated;
        }
        catch (Exception)
        {
            MessageBox(0, "起動中のLiveWallpaperへ接続できませんでした。既存のアプリを確認してから再度起動してください。",
                "LiveWallpaper", 0x10);
            Exit();
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Closed += (_, _) =>
        {
            _instance!.Activated -= Instance_Activated;
            MainWindow = null;
        };
        MainWindow.Activate();
    }

    private void Instance_Activated(object? sender, AppActivationArguments args)
        => _dispatcher?.TryEnqueue(() =>
        {
            MainWindow?.ShowSettings();
        });

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint owner, string text, string caption, uint type);
}
