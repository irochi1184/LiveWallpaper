using Microsoft.UI.Xaml.Media.Imaging;
using global::Windows.Graphics.Imaging;
using global::Windows.Storage;

namespace LiveWallpaper.App.Services;

internal static class WallpaperImageLoader
{
    public static async Task<BitmapImage> LoadAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.DecoderInformation.CodecId != BitmapDecoder.PngDecoderId
            && decoder.DecoderInformation.CodecId != BitmapDecoder.JpegDecoderId)
            throw new InvalidDataException("PNGまたはJPEG画像を選択してください。");
        var largest = Math.Max(decoder.PixelWidth, decoder.PixelHeight);
        if (largest == 0) throw new InvalidDataException("画像のサイズを取得できません。");
        // Bound decoded memory while preserving aspect ratio. Decode once and
        // share the source between the preview and desktop on the UI thread.
        var bitmap = new BitmapImage();
        if (largest > 4096)
            bitmap.DecodePixelWidth = Math.Max(1, (int)(decoder.PixelWidth * (4096.0 / largest)));
        stream.Seek(0);
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}
