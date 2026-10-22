namespace Avers.Domain;

/// <summary>
/// Неустойка по договору (п. 2.3): 1 % от долга за месяц за каждый день просрочки. Оплата — до 10 числа
/// следующего месяца (п. 5.2), просрочка считается от этого дня до даты расчёта (как в расчёте юристов).
/// </summary>
public static class Penalty
{
    public const decimal PercentPerDay = 1m;

    public sealed record Line(int Year, int Month, decimal Debt, int Days, decimal Amount);

    /// <summary>Месяцы с долгом на дату расчёта (<see cref="Billing.Due"/>); частичная оплата уменьшает долг месяца.</summary>
    public static List<Line> Calculate(IEnumerable<YearPayment> payments, DateOnly date) =>
        Billing.Due(payments, date)
            .Where(p => p.Tariff > (p.Amount ?? 0))
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .Select(p =>
            {
                var debt = p.Tariff - (p.Amount ?? 0);
                var days = Math.Max(0, date.DayNumber - new DateOnly(p.Year, p.Month, 10).AddMonths(1).DayNumber);
                return new Line(p.Year, p.Month, debt, days, Math.Round(debt * PercentPerDay / 100 * days, 2));
            })
            .ToList();

    /// <summary>Неустойка к взысканию: юристы снижают её до суммы основного долга (ст. 333 ГК РФ).</summary>
    public static decimal Claimed(IReadOnlyCollection<Line> lines) =>
        Math.Min(lines.Sum(l => l.Amount), lines.Sum(l => l.Debt));
}
