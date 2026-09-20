namespace LiveWallpaper.Core.Models;

public sealed class ClockSettings
{
    public bool ShowSeconds { get; set; } = true;

    public bool Use24HourClock { get; set; } = true;

    public bool ShowDate { get; set; }

    public double FontSize { get; set; } = 96;

    public double Opacity { get; set; } = 0.92;

    public string FontFamily { get; set; } = "Segoe UI Variable Display";

    public string GetTimeFormat()
        => Use24HourClock
            ? ShowSeconds ? "HH:mm:ss" : "HH:mm"
            : ShowSeconds ? "hh:mm:ss tt" : "hh:mm tt";
}
