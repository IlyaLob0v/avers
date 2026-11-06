using SkiaSharp;

namespace Avers.Server.Desktop;

/// <summary>
/// Схема «только на экране» (REQ-MECH-02): сервер отдаёт не исходный файл, а картинку с наложенным косым знаком
/// «Аверс · логин · дата и время», как рисует страница телефона.
/// </summary>
public static class SchemeWatermark
{
    private static readonly SKTypeface Typeface = SKTypeface.FromStream(
        typeof(SchemeWatermark).Assembly.GetManifestResourceStream("Avers.Server.Watermark.ttf")!);

    public static string Text(string login, DateTimeOffset at) => $"Аверс · {login} · {at:dd.MM.yyyy HH:mm}";

    /// <summary>Картинка в том же формате (PNG или JPEG) со знаком; null — файл не читается как картинка.</summary>
    public static byte[]? Apply(byte[] image, string format, string text)
    {
        using var source = SKBitmap.Decode(image);
        if (source is null)
            return null;

        using var result = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(result))
        {
            canvas.DrawBitmap(source, 0, 0);
            var size = Math.Max(16f, source.Width / 25f);
            using var font = new SKFont(Typeface, size);
            using var paint = new SKPaint { Color = new SKColor(200, 0, 0, 90), IsAntialias = true };
            var step = font.MeasureText(text) + size * 2;
            canvas.RotateDegrees(-30);
            for (var y = 0f; y < source.Height * 1.6f; y += size * 4)
                for (var x = -source.Width * 1f; x < source.Width * 1.2f; x += step)
                    canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
        }
        using var data = result.Encode(format == "PNG" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }
}
