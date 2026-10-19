using Avers.Domain;

namespace Avers.Data;

/// <summary>Загрузка картинок схем СКПТ: JPG или PNG до <see cref="MaxBytes"/>, с записью в журнал изменений.</summary>
public static class SchemeFiles
{
    public const int MaxBytes = 10 * 1024 * 1024;

    /// <summary>Формат по первым байтам файла; null — не JPEG и не PNG.</summary>
    public static string? ImageFormat(byte[] content) => content switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "JPEG",
        [0x89, 0x50, 0x4E, 0x47, ..] => "PNG",
        _ => null
    };

    /// <summary>Текст ошибки для сотрудника или null, если файл подходит.</summary>
    public static string? Check(byte[] content) =>
        content.Length > MaxBytes ? "Файл схемы — не больше 10 МБ."
        : ImageFormat(content) is null ? "Схема должна быть картинкой JPG или PNG."
        : null;

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
}
