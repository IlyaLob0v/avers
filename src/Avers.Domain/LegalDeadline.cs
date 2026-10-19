namespace Avers.Domain;

/// <summary>Этап работы юристов и его срок от даты события (направления претензии, получения приказа и т. п.).</summary>
public sealed record LegalStage(string Title, int Days = 0, int Months = 0, int Years = 0);

/// <summary>
/// Калькулятор обратного отсчёта для юристов. Срок считается по ГК РФ (ст. 191–193): со следующего дня после события;
/// окончание в нерабочий день переносится на ближайший рабочий день по <see cref="WorkCalendar"/>.
/// </summary>
public static class LegalDeadline
{
    public static IReadOnlyList<LegalStage> Stages { get; } =
    [
        new("Ответ на претензию", Days: 30),
        new("Возражения на судебный приказ", Days: 10),
        new("Обжалование решения суда", Months: 1),
        new("Срок исковой давности", Years: 3)
    ];

    public static DateOnly Due(LegalStage stage, DateOnly start)
    {
        var due = start.AddDays(stage.Days).AddMonths(stage.Months).AddYears(stage.Years);
        while (!WorkCalendar.IsWorkingDay(due))
            due = due.AddDays(1);
        return due;
    }

    public static string Remaining(DateOnly due, DateOnly today)
    {
        var days = due.DayNumber - today.DayNumber;
        var text = days switch
        {
            > 0 => $"осталось {days} дн. (до {due:dd.MM.yyyy})",
            0 => $"последний день — сегодня, {due:dd.MM.yyyy}",
            _ => $"срок истёк {due:dd.MM.yyyy}, {-days} дн. назад"
        };
        return WorkCalendar.HasTransfers(due.Year)
            ? text
            : $"{text}; переносы выходных на {due.Year} г. не внесены, проверьте по календарю";
    }
}
