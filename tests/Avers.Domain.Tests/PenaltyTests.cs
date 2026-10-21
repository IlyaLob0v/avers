using Avers.Domain;

namespace Avers.Domain.Tests;

public class PenaltyTests
{
    private static YearPayment Month(int year, int month, decimal tariff, decimal? amount = null) =>
        new() { Year = year, Month = month, Tariff = tariff, Amount = amount };

    [Fact]
    public void Calculate_MatchesLawyersSheet()
    {
        // «Расчет неустойки.xls» юристов: расчёт на 01.01.2024.
        var payments = new[]
        {
            Month(2022, 3, 100, 76), Month(2023, 8, 100, 23.68m), Month(2023, 11, 100), Month(2023, 12, 100), Month(2024, 1, 0)
        };

        var lines = Penalty.Calculate(payments, new DateOnly(2024, 1, 1));

        Assert.Equal(
            [(2022, 3, 24m, 631, 151.44m), (2023, 8, 76.32m, 113, 86.24m), (2023, 11, 100m, 22, 22m), (2023, 12, 100m, 0, 0m)],
            lines.Select(l => (l.Year, l.Month, l.Debt, l.Days, l.Amount)));
    }

    [Fact]
    public void Calculate_SkipsPaidAndFutureMonths()
    {
        var payments = new[] { Month(2026, 1, 150, 150), Month(2026, 2, 150, 0), Month(2026, 11, 150) };

        Assert.Equal([(2026, 2)], Penalty.Calculate(payments, new DateOnly(2026, 10, 21)).Select(l => (l.Year, l.Month)));
    }

    [Fact]
    public void Claimed_IsReducedToDebt()
    {
        var lines = Penalty.Calculate([Month(2025, 1, 150), Month(2026, 9, 150)], new DateOnly(2026, 10, 21));

        Assert.Equal(300m, lines.Sum(l => l.Debt));
        Assert.Equal(300m, Penalty.Claimed(lines));
    }
}
