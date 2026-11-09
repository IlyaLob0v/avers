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
        using var codec = SKCodec.Create(new SKMemoryStream(image));
        if (codec is null)
            return null;
        using var source = SKBitmap.Decode(codec);
        if (source is null)
            return null;

        // Снимок с телефона хранит поворот в EXIF, а при перекодировании EXIF теряется: поворачиваем сами пиксели.
        var origin = codec.EncodedOrigin;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int w = source.Width, h = source.Height;
        int width = swap ? h : w, height = swap ? w : h;

        using var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(result))
        {
            canvas.SetMatrix(Orientation(origin, w, h));
            canvas.DrawBitmap(source, 0, 0);
            canvas.ResetMatrix();

            var size = Math.Max(16f, width / 25f);
            using var font = new SKFont(Typeface, size);
            using var paint = new SKPaint { Color = new SKColor(200, 0, 0, 90), IsAntialias = true };
            var step = font.MeasureText(text) + size * 2;
            // Поворот вокруг центра и поле размером с диагональ: знак покрывает и узкую длинную схему целиком.
            var half = MathF.Sqrt((float)width * width + (float)height * height) / 2;
            canvas.Translate(width / 2f, height / 2f);
            canvas.RotateDegrees(-30);
            for (var y = -half; y < half + size; y += size * 4)
                for (var x = -half; x < half; x += step)
                    canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
        }
        using var data = result.Encode(format == "PNG" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>Переводит пиксели исходника (w × h) в положение, в котором снимок показывают просмотрщики.</summary>
    private static SKMatrix Orientation(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity,
    };
}
