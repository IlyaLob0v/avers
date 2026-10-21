using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Журнал изменений оплат, лицевых счетов, паспортов домов, сотрудников, заявок, графиков ППР и настроек. Запись добавляется
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

    /// <summary>Поля заявки АДС, которые попадают в журнал при правке.</summary>
    public static (string Fault, string Mechanic, string WorkDone, string Risers, TicketKind Kind, TicketStatus Status) TicketState(WorkTicket t) =>
        (t.Fault, t.Mechanic, t.WorkDone, t.RisersNote, t.Kind, t.Status);

    /// <summary>Правка заявки АДС: что поменялось по сравнению с <paramref name="before"/>. Приём заявки виден в ней самой (кто и когда принял).</summary>
    public static void TicketEdit(AversDbContext db, DateTimeOffset at, string who, WorkTicket t,
        (string Fault, string Mechanic, string WorkDone, string Risers, TicketKind Kind, TicketStatus Status) before) =>
        Add(db, at, who, "Заявка АДС", $"№ {t.Number}", Diff(
            ("Неисправность", before.Fault, t.Fault),
            ("Радиомеханик", before.Mechanic, t.Mechanic),
            ("Что сделано", before.WorkDone, t.WorkDone),
            ("Стояки", before.Risers, t.RisersNote),
            ("Вид", Billing.KindTitle(before.Kind), Billing.KindTitle(t.Kind)),
            ("Выполнена", Billing.StatusTitle(before.Status), Billing.StatusTitle(t.Status))));

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
