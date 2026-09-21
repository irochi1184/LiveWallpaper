using LiveWallpaper.Core.Models;
using LiveWallpaper.Core.Services;

internal static class SettingsTests
{
    internal static void Run()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LiveWallpaper-settings-test-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            DefaultsAndRoundTrip(root);
            DamagedFileRecovery(root);
            NormalizeUnsafeValues();
            SaveFailurePreservesSettings(root);
            Console.WriteLine("PASS: settings defaults, restart round-trip, backup, partial/invalid JSON, normalization, failed atomic save");
        }
        finally
        {
            // This unique directory was created by this test; never touch the user's settings.
            if (!root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(root).StartsWith("LiveWallpaper-settings-test-", StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid test cleanup path.");
            Directory.Delete(root, recursive: true);
        }
    }

    private static void DefaultsAndRoundTrip(string root)
    {
        var path = Path.Combine(root, "round-trip", "settings.json");
        var store = new ClockSettingsStore(path);
        Equal(new ClockSettings(), store.Load().Settings, "first launch defaults");
        Check(!File.Exists(path), "loading defaults does not write");
        var first = new ClockSettings
        {
            ShowSeconds = false, Use24HourClock = false, FontSize = 144,
            Opacity = 0.63, ColorRgb = "#33AAFF", Position = ClockPosition.BottomRight, Margin = 72
        };
        store.Save(first);
        var restarted = new ClockSettingsStore(path);
        Equal(first, restarted.Load().Settings, "all settings survive a new store instance");
        Check(restarted.Load().Warning is null, "valid settings load without warning");
        store.Save(first with { ShowSeconds = true, Position = ClockPosition.TopLeft });
        Equal(first, new ClockSettingsStore(path + ".bak").Load().Settings, "previous settings backed up");
        Equal(ClockPosition.TopLeft, restarted.Load().Settings.Position, "second save replaces file");
        File.WriteAllText(path, """{"version":1,"clock":{"fontSize":120}}""");
        Equal(new ClockSettings { FontSize = 120 }, restarted.Load().Settings, "missing fields use compatible defaults");
    }

    private static void DamagedFileRecovery(string root)
    {
        var path = Path.Combine(root, "damaged.json");
        var store = new ClockSettingsStore(path);
        foreach (var broken in new[] { "{broken", "null", """{"version":99,"clock":{}}""",
                     """{"version":1,"clock":null}""", """{"clock":{"position":"UnknownFuturePosition"}}""" })
        {
            File.WriteAllText(path, broken);
            var result = store.Load();
            Equal(new ClockSettings(), result.Settings, "invalid file falls back to defaults");
            Check(result.Warning != null, "invalid file warning");
            Equal(broken, File.ReadAllText(path), "load does not overwrite original");
        }
        var original = File.ReadAllText(path);
        store.Save(new ClockSettings { FontSize = 80 });
        Equal(original, File.ReadAllText(path + ".bak"), "damaged source preserved on explicit save");
        Equal(80d, store.Load().Settings.FontSize, "can recover by saving new settings");

        File.WriteAllText(path, """{"clock":{"fontSize":-5,"opacity":9,"margin":999,"colorRgb":null,"fontFamily":null,"position":999}}""");
        var normalized = store.Load();
        Check(normalized.Warning != null, "normalization warning");
        Equal(24d, normalized.Settings.FontSize, "font lower bound");
        Equal(1d, normalized.Settings.Opacity, "opacity upper bound");
        Equal(256d, normalized.Settings.Margin, "margin upper bound");
        Equal("#FFFFFF", normalized.Settings.ColorRgb, "invalid color fallback");
        Equal(ClockPosition.Center, normalized.Settings.Position, "unknown enum fallback");
    }

    private static void NormalizeUnsafeValues()
    {
        var invalid = new ClockSettings
        {
            FontSize = double.NaN, Opacity = double.PositiveInfinity, Margin = double.NegativeInfinity,
            ColorRgb = "#abcdef", FontFamily = " "
        };
        var normalized = invalid.NormalizedCopy();
        Equal(96d, normalized.FontSize, "NaN font fallback");
        Equal(0.92, normalized.Opacity, "infinite opacity fallback");
        Equal(48d, normalized.Margin, "infinite margin fallback");
        Equal("#ABCDEF", normalized.ColorRgb, "canonical color");
        Check(double.IsNaN(invalid.FontSize), "normalization does not mutate caller");
        Equal(0.1, (new ClockSettings { Opacity = 0 }).NormalizedCopy().Opacity, "clock remains visible");
        Equal(240d, (new ClockSettings { FontSize = 10000 }).NormalizedCopy().FontSize, "font upper bound");
        Equal(0d, (new ClockSettings { Margin = -1 }).NormalizedCopy().Margin, "margin lower bound");
    }

    private static void SaveFailurePreservesSettings(string root)
    {
        var path = Path.Combine(root, "locked.json");
        var store = new ClockSettingsStore(path);
        store.Save(new ClockSettings { FontSize = 110 });
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Check(store.Load().Warning != null, "read failure is reported");
            try
            {
                store.Save(new ClockSettings { FontSize = 200 });
                throw new Exception("Expected save to fail while destination is locked.");
            }
            catch (IOException) { }
        }
        Equal(110d, store.Load().Settings.FontSize, "failed replacement preserves saved settings");
        Equal(0, Directory.GetFiles(root, ".settings-*.tmp").Length, "temporary file cleaned up after failure");
        store.Save(new ClockSettings { FontSize = 200 });
        Equal(200d, store.Load().Settings.FontSize, "retry succeeds after write failure");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }

    private static void Equal<T>(T expected, T actual, string message)
        => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected {expected}, got {actual}");
}
