using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Скан в списке документов абонента, без самого файла.</summary>
public sealed record DocumentInfo(int Id, string Kind, string Name, string Format, DateOnly AddedAt, string AddedBy);

/// <summary>Сканы документов абонента: PDF, JPG или PNG до <see cref="MaxBytes"/>, с записью в журнал изменений.</summary>
public static class SubscriberDocuments
{
    public const int MaxBytes = 20 * 1024 * 1024;

    public static IReadOnlyList<string> Kinds { get; } = ["Договор", "Решение суда", "Постановление ОСП", "Другое"];

    /// <summary>Формат по первым байтам файла; null — не PDF, не JPEG и не PNG.</summary>
    public static string? Format(byte[] content) => content switch
    {
        [0x25, 0x50, 0x44, 0x46, ..] => "PDF",
        _ => SchemeFiles.ImageFormat(content)
    };

    public static string ContentType(string format) => format == "PDF" ? "application/pdf" : SchemeFiles.ContentType(format);

    public static string Extension(string format) => format switch { "PDF" => ".pdf", "PNG" => ".png", _ => ".jpg" };

    /// <summary>Текст ошибки для сотрудника или null, если документ подходит.</summary>
    public static string? Check(string kind, string name, byte[] content) =>
        !Kinds.Contains(kind) ? "Выберите вид документа."
        : string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 ? "Название документа — от 1 до 200 символов."
        : content.Length > MaxBytes ? "Файл — не больше 20 МБ."
        : Format(content) is null ? "Скан должен быть файлом PDF, JPG или PNG."
        : null;

    public static List<DocumentInfo> List(AversDbContext db, int subscriberId) =>
        db.SubscriberDocuments.AsNoTracking()
            .Where(d => d.SubscriberId == subscriberId)
            .OrderBy(d => d.Id)
            .Select(d => new DocumentInfo(d.Id, d.Kind, d.Name, d.Format, d.AddedAt, d.AddedBy))
            .ToList();

    public static SubscriberDocument? File(AversDbContext db, int id) =>
        db.SubscriberDocuments.AsNoTracking().FirstOrDefault(d => d.Id == id);

    public static void Add(AversDbContext db, Subscriber subscriber, string kind, string name, byte[] content, DateOnly today, string who, DateTimeOffset at)
    {
        db.SubscriberDocuments.Add(new SubscriberDocument
        {
            SubscriberId = subscriber.Id,
            Kind = kind,
            Name = name.Trim(),
            Format = Format(content)!,
            AddedAt = today,
            AddedBy = who,
            Content = content
        });
        ChangeLog.Add(db, at, who, "Скан документа", ChangeLog.Account(subscriber), [$"Добавлен: {kind}, {name.Trim()}"]);
        db.SaveChanges();
    }

    /// <summary>Удаляет скан; false — такого нет.</summary>
    public static bool Delete(AversDbContext db, int id, string who, DateTimeOffset at)
    {
        var doc = db.SubscriberDocuments.Include(d => d.Subscriber).FirstOrDefault(d => d.Id == id);
        if (doc is null)
            return false;
        db.SubscriberDocuments.Remove(doc);
        ChangeLog.Add(db, at, who, "Скан документа", ChangeLog.Account(doc.Subscriber), [$"Удалён: {doc.Kind}, {doc.Name}"]);
        db.SaveChanges();
        return true;
    }
}
