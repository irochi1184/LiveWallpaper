using System.Globalization;

namespace LiveWallpaper.Core.Models;

public enum ClockPosition
{
    Center,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public sealed record ClockSettings
{
    public bool ShowSeconds { get; set; } = true;
    public bool Use24HourClock { get; set; } = true;
    public bool ShowDate { get; set; }
    public double FontSize { get; set; } = 96;
    public double Opacity { get; set; } = 0.92;
    public string FontFamily { get; set; } = "Segoe UI Variable Display";
    public string ColorRgb { get; set; } = "#FFFFFF";
    public ClockPosition Position { get; set; } = ClockPosition.Center;
    public double Margin { get; set; } = 48;

    public string GetTimeFormat()
        => Use24HourClock
            ? ShowSeconds ? "HH:mm:ss" : "HH:mm"
            : ShowSeconds ? "hh:mm:ss tt" : "hh:mm tt";

    // Treat disk contents and UI values as input, not trusted rendering values.
    public ClockSettings NormalizedCopy() => this with
    {
        FontSize = ClampFinite(FontSize, 24, 240, 96),
        Opacity = ClampFinite(Opacity, 0.1, 1, 0.92),
        Margin = ClampFinite(Margin, 0, 256, 48),
        ColorRgb = IsValidColor(ColorRgb) ? ColorRgb.ToUpperInvariant() : "#FFFFFF",
        Position = Enum.IsDefined(Position) ? Position : ClockPosition.Center,
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 128
            ? "Segoe UI Variable Display" : FontFamily.Trim()
    };

    private static double ClampFinite(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static bool IsValidColor(string? value)
        => value is { Length: 7 } && value[0] == '#'
           && uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
}
