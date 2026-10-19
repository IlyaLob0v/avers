namespace Avers.Domain;

/// <summary>
/// Производственный календарь РФ: выходные, праздники по ст. 112 ТК РФ и переносы по постановлениям Правительства.
/// Для лет без постановления учитываются только выходные и праздники по ст. 112.
/// </summary>
public static class WorkCalendar
{
    private static readonly (int Month, int Day)[] Article112 =
        [(1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6), (1, 7), (1, 8), (2, 23), (3, 8), (5, 1), (5, 9), (6, 12), (11, 4)];

    // Нерабочие будние дни по постановлениям: 2025 — № 1335 от 04.10.2024, 2026 — № 1466 от 24.09.2025, 2027 — № 1187 от 17.09.2026.
    private static readonly HashSet<DateOnly> DaysOff =
    [
        new(2025, 1, 1), new(2025, 1, 2), new(2025, 1, 3), new(2025, 1, 6), new(2025, 1, 7), new(2025, 1, 8),
        new(2025, 5, 1), new(2025, 5, 2), new(2025, 5, 8), new(2025, 5, 9), new(2025, 6, 12), new(2025, 6, 13),
        new(2025, 11, 3), new(2025, 11, 4), new(2025, 12, 31),

        new(2026, 1, 1), new(2026, 1, 2), new(2026, 1, 5), new(2026, 1, 6), new(2026, 1, 7), new(2026, 1, 8), new(2026, 1, 9),
        new(2026, 2, 23), new(2026, 3, 9), new(2026, 5, 1), new(2026, 5, 11), new(2026, 6, 12), new(2026, 11, 4), new(2026, 12, 31),

        new(2027, 1, 1), new(2027, 1, 4), new(2027, 1, 5), new(2027, 1, 6), new(2027, 1, 7), new(2027, 1, 8),
        new(2027, 2, 22), new(2027, 2, 23), new(2027, 3, 8), new(2027, 5, 3), new(2027, 5, 10), new(2027, 6, 14),
        new(2027, 11, 4), new(2027, 11, 5), new(2027, 12, 31)
    ];

    private static readonly HashSet<DateOnly> WorkingWeekends = [new(2025, 11, 1), new(2027, 2, 20)];

    public static bool HasTransfers(int year) => year is >= 2025 and <= 2027;

    public static bool IsWorkingDay(DateOnly date)
    {
        if (WorkingWeekends.Contains(date))
            return true;
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;
        return HasTransfers(date.Year) ? !DaysOff.Contains(date) : !Article112.Contains((date.Month, date.Day));
    }
}
