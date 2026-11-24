using Avers.Domain;
using SkiaSharp;

namespace Avers.Data;

/// <summary>Загрузка картинок схем СКПТ: JPG или PNG до <see cref="MaxBytes"/>, с записью в журнал изменений.</summary>
public static class SchemeFiles
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public const long MaxPixels = 16_000_000;

    public static bool SafeDimensions(int width, int height) => width > 0 && height > 0 && width <= 16_384 && height <= 16_384 && (long)width * height <= MaxPixels;

    /// <summary>Формат по первым байтам файла; null — не JPEG и не PNG.</summary>
    public static string? ImageFormat(byte[]? content) => content switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "JPEG",
        [0x89, 0x50, 0x4E, 0x47, ..] => "PNG",
        _ => null
    };

    /// <summary>Текст ошибки для сотрудника или null, если файл подходит.</summary>
    public static string? Check(byte[]? content) => CheckImage(content, MaxBytes);

    public static string? CheckImage(byte[]? content, int maxBytes)
    {
        if (content is null || content.Length == 0)
            return "Выберите файл картинки.";
        if (content.Length > maxBytes)
            return $"Файл — не больше {maxBytes / 1024 / 1024} МБ.";
        if (ImageFormat(content) is null)
            return "Схема должна быть картинкой JPG или PNG.";
        using var stream = new SKMemoryStream(content);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png))
            return "Картинка повреждена или имеет неподдерживаемый формат.";
        if (!SafeDimensions(codec.Info.Width, codec.Info.Height))
            return "Картинка слишком большая: не больше 16 миллионов пикселей и 16384 по каждой стороне.";
        using var bitmap = new SKBitmap(codec.Info);
        return codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success
            ? null : "Картинка повреждена или загружена не полностью.";
    }

    public static string? CheckName(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 ? "Название схемы — от 1 до 200 символов." : null;

    public static string ContentType(string format) => format == "PNG" ? "image/png" : "image/jpeg";

    public static byte[]? Content(AversDbContext db, int schemeId) =>
        db.HouseSchemeFiles.Where(f => f.HouseSchemeId == schemeId).Select(f => f.Content).FirstOrDefault();

    public static void Add(AversDbContext db, House house, string name, byte[] content, DateOnly today, string who, DateTimeOffset at)
    {
        var scheme = new HouseScheme { HouseId = house.Id, Name = name.Trim(), Format = ImageFormat(content)!, UpdatedAt = today };
        db.HouseSchemeFiles.Add(new HouseSchemeFile { Scheme = scheme, Content = content });
        ChangeLog.Add(db, at, who, "Схема СКПТ", house.AddressLine, [$"Добавлена: {scheme.Name}"]);
        db.SaveChanges();
    }

    public static void Replace(AversDbContext db, HouseScheme scheme, byte[] content, DateOnly today, string who, DateTimeOffset at)
    {
        var file = db.HouseSchemeFiles.FirstOrDefault(f => f.HouseSchemeId == scheme.Id);
        if (file is null)
            db.HouseSchemeFiles.Add(new HouseSchemeFile { HouseSchemeId = scheme.Id, Content = content });
        else
            file.Content = content;
        scheme.Format = ImageFormat(content)!;
        scheme.UpdatedAt = today;
        ChangeLog.Add(db, at, who, "Схема СКПТ", scheme.House.AddressLine, [$"Заменён файл: {scheme.Name}"]);
        db.SaveChanges();
    }

    public static void Rename(AversDbContext db, HouseScheme scheme, string name, string who, DateTimeOffset at)
    {
        ChangeLog.Add(db, at, who, "Схема СКПТ", scheme.House.AddressLine, ChangeLog.Diff(("Название", scheme.Name, name)));
        scheme.Name = name.Trim();
        db.SaveChanges();
    }

    /// <summary>Удаляет схему вместе с картинкой.</summary>
    public static void Delete(AversDbContext db, HouseScheme scheme, string who, DateTimeOffset at)
    {
        db.HouseSchemes.Remove(scheme);
        ChangeLog.Add(db, at, who, "Схема СКПТ", scheme.House.AddressLine, [$"Удалена: {scheme.Name}"]);
        db.SaveChanges();
    }
}
