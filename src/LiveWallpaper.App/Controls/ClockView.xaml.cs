using System.Globalization;
using LiveWallpaper.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LiveWallpaper.App.Controls;

public sealed partial class ClockView : UserControl
{
    private ClockSettings _settings = new();
    private string _lastDisplayedTime = string.Empty;

    public ClockView()
    {
        InitializeComponent();
        ApplySettings(_settings);
    }

    public void ApplySettings(ClockSettings settings)
    {
        _settings = settings.NormalizedCopy();
        ClockText.FontSize = _settings.FontSize;
        ClockText.FontFamily = new FontFamily(_settings.FontFamily);
        ClockText.Foreground = new SolidColorBrush(ParseColor(_settings.ColorRgb));
        ClockText.Opacity = _settings.Opacity;
        ClockContainer.HorizontalAlignment = _settings.Position switch
        {
            ClockPosition.TopLeft or ClockPosition.BottomLeft => HorizontalAlignment.Left,
            ClockPosition.TopRight or ClockPosition.BottomRight => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center
        };
        ClockContainer.VerticalAlignment = _settings.Position switch
        {
            ClockPosition.TopLeft or ClockPosition.TopRight => VerticalAlignment.Top,
            ClockPosition.BottomLeft or ClockPosition.BottomRight => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };
        UpdateMargin();
        _lastDisplayedTime = string.Empty;
        UpdateTime();
    }

    public void UpdateTime()
    {
        var text = DateTime.Now.ToString(_settings.GetTimeFormat(), CultureInfo.InvariantCulture);
        if (text == _lastDisplayedTime)
            return;
        _lastDisplayedTime = text;
        ClockText.Text = text;
    }

    public static global::Windows.UI.Color ParseColor(string rgb)
        => global::Windows.UI.Color.FromArgb(255,
            byte.Parse(rgb.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(rgb.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(rgb.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private void ClockRoot_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMargin();

    private void UpdateMargin()
    {
        // Keep a usable drawing area even after moving to a small display.
        var margin = _settings.Position == ClockPosition.Center ? 0 : _settings.Margin;
        if (ClockRoot.ActualWidth > 0 && ClockRoot.ActualHeight > 0)
            margin = Math.Min(margin, Math.Min(ClockRoot.ActualWidth, ClockRoot.ActualHeight) / 4);
        ClockRoot.Padding = new Thickness(margin);
    }
}
