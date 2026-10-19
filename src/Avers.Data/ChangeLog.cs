using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Журнал изменений оплат, лицевых счетов, паспортов домов и сотрудников. Запись добавляется
/// в тот же SaveChanges, что и сама правка: нет правки — нет записи, и наоборот.
/// </summary>
public static class ChangeLog
{
    /// <summary>Сколько последних записей показывает экран журнала.</summary>
    public const int Shown = 1000;

    public static List<ChangeLogEntry> Latest(AversDbContext db) =>
        db.ChangeLog.AsNoTracking().OrderByDescending(e => e.Id).Take(Shown).ToList();

    public static string Who(AversDbContext db, int userId) =>
        db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName + " (" + u.Login + ")").FirstOrDefault() ?? "";

    /// <summary>Добавляет запись, если есть изменённые поля.</summary>
    public static void Add(AversDbContext db, DateTimeOffset at, string who, string action, string target, IReadOnlyList<string> changes)
    {
        if (changes.Count == 0)
            return;
        db.ChangeLog.Add(new ChangeLogEntry
        {
            At = at,
            UserName = who,
            Action = action,
            Target = target,
            Changes = string.Join("\n", changes)
        });
    }

    /// <summary>Строки «Поле: было → стало» для полей, где значение поменялось. Пустое значение — «—».</summary>
    public static List<string> Diff(params (string Field, object? Before, object? After)[] fields) =>
        fields.Select(f => (f.Field, Before: Show(f.Before), After: Show(f.After)))
            .Where(f => f.Before != f.After)
            .Select(f => $"{f.Field}: {f.Before} → {f.After}")
            .ToList();

    public static string Account(Subscriber s) => $"л/с {s.Account}, {s.ShortName}, кв. {s.Apartment}";

    private static string Show(object? value) => value switch
    {
        null => "—",
        string s when string.IsNullOrWhiteSpace(s) => "—",
        string s => s.Trim(),
        bool b => b ? "да" : "нет",
        DateOnly d => d.ToString("dd.MM.yyyy"),
        decimal m => m.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")),
        _ => value.ToString() ?? "—"
    };
}
