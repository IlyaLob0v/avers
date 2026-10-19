namespace Avers.Domain;

/// <summary>
/// График планово-предупредительных работ: в базе хранится план или отметка о выполнении,
/// просрочка считается по рабочей дате — план прошедшего месяца без отметки.
/// </summary>
public static class PprSchedule
{
    public static PprStatus Effective(PprEntry entry, DateOnly today) =>
        entry.Status is PprStatus.Planned or PprStatus.Overdue
            ? IsPast(entry.Year, entry.Month, today) ? PprStatus.Overdue : PprStatus.Planned
            : entry.Status;

    /// <summary>Щелчок по ячейке графика: пусто → план → выполнено → пусто.</summary>
    public static PprStatus Next(PprStatus status) => status switch
    {
        PprStatus.Empty => PprStatus.Planned,
        PprStatus.Planned or PprStatus.Overdue => PprStatus.Done,
        _ => PprStatus.Empty
    };

    /// <summary>Какой датой отметить выполнение месяца: сегодня — для текущего месяца, иначе последним днём месяца.</summary>
    public static DateOnly DoneDate(int year, int month, DateOnly today)
    {
        var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return end < today ? end : today;
    }

    /// <summary>Доля выполненных работ среди тех, чей срок наступил к рабочей дате, в процентах.</summary>
    public static int DonePercent(IEnumerable<PprEntry> entries, DateOnly today)
    {
        var due = entries.Where(e => e.Status != PprStatus.Empty && !IsFuture(e.Year, e.Month, today)).ToList();
        return due.Count == 0 ? 0 : (int)Math.Round(100.0 * due.Count(e => e.Status == PprStatus.Done) / due.Count);
    }

    /// <summary>План на следующий год: те же месяцы по тем же домам, все без отметки о выполнении.</summary>
    public static IEnumerable<PprEntry> PlanForNextYear(IEnumerable<PprEntry> year) =>
        year.Where(e => e.Status != PprStatus.Empty)
            .Select(e => new PprEntry { HouseId = e.HouseId, Year = e.Year + 1, Month = e.Month, Status = PprStatus.Planned });

    private static bool IsPast(int year, int month, DateOnly today) =>
        year < today.Year || (year == today.Year && month < today.Month);

    private static bool IsFuture(int year, int month, DateOnly today) =>
        year > today.Year || (year == today.Year && month > today.Month);
}
