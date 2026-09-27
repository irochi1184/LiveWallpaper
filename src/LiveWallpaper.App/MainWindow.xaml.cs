using LiveWallpaper.Core.Models;
using LiveWallpaper.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using LiveWallpaper.App.Services;
using LiveWallpaper.Windows.Services;
using Microsoft.UI.Windowing;

namespace LiveWallpaper.App;

public sealed partial class MainWindow : Window
{
    private readonly ClockSettingsStore _settingsStore = new();
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly DispatcherQueueTimer _previewTimer;
    private ClockSettings _settings = new();
    private WallpaperSettings _background = new();
    private BitmapImage? _backgroundImage;
    private bool _backgroundBusy;
    private WallpaperWindow? _wallpaperWindow;
    private bool _ready;
    private bool _updatingControls;
    private bool _dirty;
    private bool _closing;
    private bool _exitRequested;
    private TrayIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        Title = "LiveWallpaper";
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(820, 920));
        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(400);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += SaveTimer_Tick;
        _previewTimer = DispatcherQueue.CreateTimer();
        _previewTimer.Interval = TimeSpan.FromMilliseconds(250);
        _previewTimer.Tick += PreviewTimer_Tick;
        var loaded = _settingsStore.Load();
        _settings = loaded.Settings;
        _background = loaded.Wallpaper;
        PopulateControls();
        SaveStatusText.Text = loaded.Warning ?? "設定は自動保存され、次回起動時に復元されます。";
        _ready = true;
        _previewTimer.Start();
        Closed += MainWindow_Closed;
        AppWindow.Closing += AppWindow_Closing;
        InitializeTray();
        ((FrameworkElement)Content).Loaded += MainContent_Loaded;
    }

    private void InitializeTray()
    {
        try
        {
            _trayIcon = new TrayIcon(WinRT.Interop.WindowNative.GetWindowHandle(this));
            _trayIcon.ShowRequested += () => DispatcherQueue.TryEnqueue(ShowSettings);
            _trayIcon.ExitRequested += () => DispatcherQueue.TryEnqueue(ExitApplication);
            _trayIcon.Unavailable += () => DispatcherQueue.TryEnqueue(() =>
            {
                ShowSettings();
                TrayStatusText.Text = "トレイに表示できません。画面を閉じるとアプリも終了します。";
            });
            _trayIcon.SessionEnding += () => _exitRequested = true;
        }
        catch (Exception)
        {
            TrayStatusText.Text = "トレイに表示できません。画面を閉じるとアプリも終了します。";
        }
    }

    public void ShowSettings()
    {
        if (_closing) return;
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        Activate();
        ClockPreview.UpdateTime();
        _previewTimer.Start();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitRequested || _trayIcon?.IsAvailable != true) return;
        args.Cancel = true;
        SaveSettings();
        // Keep a failed save visible so it can be retried before leaving settings.
        if (_dirty) return;
        _previewTimer.Stop();
        sender.Hide();
    }

    private void ExitApplication()
    {
        if (_closing) return;
        SaveSettings();
        if (_dirty) { ShowSettings(); return; }
        _exitRequested = true;
        Close();
    }

    private void ExitApplicationButton_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void PopulateControls()
    {
        _updatingControls = true;
        try
        {
            SecondsSwitch.IsOn = _settings.ShowSeconds;
            HourFormatSwitch.IsOn = _settings.Use24HourClock;
            FontSizeSlider.Value = _settings.FontSize;
            OpacitySlider.Value = _settings.Opacity * 100;
            ClockColorPicker.Color = Controls.ClockView.ParseColor(_settings.ColorRgb);
            PositionBox.SelectedIndex = (int)_settings.Position;
            MarginSlider.Value = _settings.Margin;
            BackgroundFitBox.SelectedIndex = (int)_background.Fit;
            BackgroundColorPicker.Color = Controls.ClockView.ParseColor(_background.BackgroundColor);
        }
        finally { _updatingControls = false; }
        UpdateAppearance();
    }

    private void ApplyControls()
    {
        if (!_ready || _updatingControls || _closing)
            return;
        var color = ClockColorPicker.Color;
        _settings = (_settings with
        {
            ShowSeconds = SecondsSwitch.IsOn,
            Use24HourClock = HourFormatSwitch.IsOn,
            FontSize = FontSizeSlider.Value,
            Opacity = OpacitySlider.Value / 100,
            ColorRgb = $"#{color.R:X2}{color.G:X2}{color.B:X2}",
            Position = (ClockPosition)PositionBox.SelectedIndex,
            Margin = MarginSlider.Value
        }).NormalizedCopy();
        UpdateAppearance();
        ScheduleSave();
    }

    private void UpdateAppearance()
    {
        FontSizeLabel.Text = $"文字サイズ：{_settings.FontSize:0}";
        OpacityLabel.Text = $"不透明度：{_settings.Opacity * 100:0}%";
        MarginLabel.Text = $"画面端からの余白：{_settings.Margin:0}";
        MarginSlider.IsEnabled = _settings.Position != ClockPosition.Center;
        ColorLabel.Text = _settings.ColorRgb;
        ColorSwatch.Background = new SolidColorBrush(Controls.ClockView.ParseColor(_settings.ColorRgb));
        ClockPreview.ApplySettings(_settings);
        _wallpaperWindow?.ApplySettings(_settings);
        UpdateBackground();
    }

    private void ScheduleSave()
    {
        _dirty = true;
        SaveStatusText.Text = "変更を反映しました。保存中…";
        RetrySaveButton.Visibility = Visibility.Collapsed;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveSettings()
    {
        if (!_dirty)
            return;
        try
        {
            _settingsStore.Save(_settings, _background);
            _dirty = false;
            if (!_closing)
            {
                SaveStatusText.Text = "設定を保存しました。";
                RetrySaveButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!_closing)
            {
                SaveStatusText.Text = "表示には反映しましたが、設定を保存できませんでした。保存を再試行してください。";
                RetrySaveButton.Visibility = Visibility.Visible;
            }
        }
    }

    private void SettingToggle_Toggled(object sender, RoutedEventArgs e) => ApplyControls();
    private void SettingSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => ApplyControls();
    private void PositionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyControls();
    private void ClockColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args) => ApplyControls();
    private void SaveTimer_Tick(DispatcherQueueTimer sender, object args) => SaveSettings();
    private void PreviewTimer_Tick(DispatcherQueueTimer sender, object args) => ClockPreview.UpdateTime();
    private void RetrySaveButton_Click(object sender, RoutedEventArgs e) => SaveSettings();

    private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = new ClockSettings();
        PopulateControls();
        ScheduleSave();
    }

    private void ShowWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wallpaperWindow != null)
            return;
        ShowWallpaperButton.IsEnabled = false;
        try
        {
            _wallpaperWindow = new WallpaperWindow(_settings);
            _wallpaperWindow.ApplyBackground(_background, _backgroundImage);
            _wallpaperWindow.Closed += WallpaperWindow_Closed;
            _wallpaperWindow.DesktopConnectionLost += WallpaperWindow_DesktopConnectionLost;
            _wallpaperWindow.ShowOnDesktop();
            CloseWallpaperButton.IsEnabled = true;
            StatusText.Text = "プライマリ画面に時計を表示しています。設定の変更はすぐに反映されます。";
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
        StatusText.Text = "時計を閉じました。設定は次回の表示にも使われます。";
    }

    private void WallpaperWindow_DesktopConnectionLost(object? sender, string message)
    {
        CloseWallpaper();
        ShowSettings();
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
        AppWindow.Closing -= AppWindow_Closing;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _saveTimer.Stop();
        _previewTimer.Stop();
        _saveTimer.Tick -= SaveTimer_Tick;
        _previewTimer.Tick -= PreviewTimer_Tick;
        // Flush a slider change even if the user closes within the debounce interval.
        SaveSettings();
        CloseWallpaper();
        _backgroundImage = null;
    }

    private async void MainContent_Loaded(object sender, RoutedEventArgs e)
    {
        ((FrameworkElement)sender).Loaded -= MainContent_Loaded;
        if (_background.ImagePath is not { } path || _closing) return;
        SetBackgroundBusy(true);
        try { await LoadBackgroundAsync(path, persist: false); }
        finally { if (!_closing) SetBackgroundBusy(false); }
    }

    private void UpdateBackground()
    {
        ClockPreview.ApplyBackground(_background, _backgroundImage);
        _wallpaperWindow?.ApplyBackground(_background, _backgroundImage);
        ImageNameText.Text = _background.ImagePath is { } path ? Path.GetFileName(path) : "画像なし";
        BackgroundColorLabel.Text = $"背景色：{_background.BackgroundColor}";
        BackgroundFitBox.IsEnabled = _backgroundImage != null;
        ClearImageButton.IsEnabled = !_backgroundBusy && _background.ImagePath != null;
    }

    private void SetBackgroundBusy(bool busy)
    {
        _backgroundBusy = busy;
        ChooseImageButton.IsEnabled = !busy;
        ClearImageButton.IsEnabled = !busy && _background.ImagePath != null;
    }

    private async void ChooseImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundBusy || _closing) return;
        SetBackgroundBusy(true);
        try
        {
            var picker = new global::Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file != null && !_closing) await LoadBackgroundAsync(file.Path, persist: true);
        }
        catch (Exception)
        {
            if (!_closing) BackgroundStatusText.Text = "画像の選択を開けませんでした。もう一度お試しください。";
        }
        finally { if (!_closing) SetBackgroundBusy(false); }
    }

    private async Task LoadBackgroundAsync(string path, bool persist)
    {
        BackgroundStatusText.Text = "画像を読み込んでいます…";
        try
        {
            var image = await WallpaperImageLoader.LoadAsync(path);
            if (_closing) return;
            // Commit only after decoding. Invalid files leave the current image
            // and saved selection intact. Fit/color edits made while loading survive.
            _backgroundImage = image;
            _background = _background with { ImagePath = path };
            UpdateBackground();
            BackgroundStatusText.Text = "画像を表示しています。画像を移動・削除すると次回読み込めなくなります。";
            if (persist) ScheduleSave();
        }
        catch (Exception)
        {
            if (!_closing) BackgroundStatusText.Text = persist
                ? "画像を読み込めませんでした。現在の背景を維持しています。別のPNG・JPEG画像を選んでください。"
                : "保存された画像を読み込めませんでした。背景色で表示します。画像を選び直してください。";
        }
    }

    private void ClearImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundBusy || _closing) return;
        _background = _background with { ImagePath = null };
        _backgroundImage = null;
        UpdateBackground();
        BackgroundStatusText.Text = "画像を外し、背景色で表示しています。";
        ScheduleSave();
    }

    private void ApplyBackgroundControls()
    {
        if (!_ready || _updatingControls || _closing) return;
        var color = BackgroundColorPicker.Color;
        _background = (_background with
        {
            Fit = (WallpaperFit)BackgroundFitBox.SelectedIndex,
            BackgroundColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        }).NormalizedCopy();
        UpdateBackground();
        ScheduleSave();
    }

    private void BackgroundFitBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyBackgroundControls();
    private void BackgroundColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs e) => ApplyBackgroundControls();
}
