using LiveWallpaper.Core.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LiveWallpaper.App.Controls;

public sealed partial class WallpaperSurface : UserControl
{
    public WallpaperSurface() => InitializeComponent();
    public void ApplySettings(ClockSettings settings) => Clock.ApplySettings(settings);
    public void UpdateTime() => Clock.UpdateTime();

    public void ApplyBackground(WallpaperSettings settings, BitmapImage? image)
    {
        var normalized = settings.NormalizedCopy();
        BackgroundLayer.Background = new SolidColorBrush(ClockView.ParseColor(normalized.BackgroundColor));
        WallpaperImage.Stretch = normalized.Fit switch
        {
            WallpaperFit.Fit => Stretch.Uniform,
            WallpaperFit.Stretch => Stretch.Fill,
            _ => Stretch.UniformToFill
        };
        WallpaperImage.Source = image;
    }
}
