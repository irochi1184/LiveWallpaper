using System.Globalization;

namespace LiveWallpaper.Core.Models;

public enum WallpaperFit { Fill, Fit, Stretch }

public sealed record WallpaperSettings
{
    public string? ImagePath { get; set; }
    public string BackgroundColor { get; set; } = "#101014";
    public WallpaperFit Fit { get; set; } = WallpaperFit.Fill;

    public WallpaperSettings NormalizedCopy() => this with
    {
        ImagePath = NormalizePath(ImagePath),
        Fit = Enum.IsDefined(Fit) ? Fit : WallpaperFit.Fill,
        BackgroundColor = BackgroundColor is { Length: 7 } && BackgroundColor[0] == '#'
            && uint.TryParse(BackgroundColor.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? BackgroundColor.ToUpperInvariant() : "#101014"
    };

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : null; }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        { return null; }
    }
}
