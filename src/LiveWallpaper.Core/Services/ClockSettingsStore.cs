using System.Text.Json;
using System.Text.Json.Serialization;
using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Core.Services;

public sealed record SettingsLoadResult(ClockSettings Settings, string? Warning = null);

/// <summary>Small, versioned settings file. Writes replace the file only after serialization succeeds.</summary>
public sealed class ClockSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter<ClockPosition>() }
    };

    public string FilePath { get; }

    public ClockSettingsStore(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiveWallpaper", "settings.json"));
    }

    public SettingsLoadResult Load()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            var document = JsonSerializer.Deserialize<SettingsDocument>(stream, JsonOptions)
                ?? throw new JsonException("Empty settings document.");
            if (document.Version != 1 || document.Clock is null)
                throw new JsonException("Unsupported settings document.");
            var normalized = document.Clock.NormalizedCopy();
            return new(normalized, normalized == document.Clock ? null : "設定の範囲外の値を補正しました。");
        }
        catch (FileNotFoundException) { return new(new ClockSettings()); }
        catch (DirectoryNotFoundException) { return new(new ClockSettings()); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Loading never overwrites a damaged or newer file. A later save
            // preserves the previous contents in settings.json.bak.
            return new(new ClockSettings(), "設定を読み込めなかったため、初期設定で起動しました。");
        }
    }

    public void Save(ClockSettings settings)
    {
        var json = JsonSerializer.Serialize(new SettingsDocument { Clock = settings.NormalizedCopy() }, JsonOptions);
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporaryFile = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryFile, json);
            if (File.Exists(FilePath))
                File.Replace(temporaryFile, FilePath, FilePath + ".bak");
            else
                File.Move(temporaryFile, FilePath);
        }
        finally
        {
            // Cleanup must not hide the original write/replace error.
            try { File.Delete(temporaryFile); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class SettingsDocument
    {
        public SettingsDocument() { }
        public int Version { get; set; } = 1;
        public ClockSettings? Clock { get; set; } = new();
    }
}
