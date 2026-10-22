using Avers.Domain;

namespace Avers.Domain.Tests;

public class BillingTests
{
    private static YearPayment Month(int month, decimal tariff, decimal? amount = null) =>
        new() { Year = 2026, Month = month, Tariff = tariff, Amount = amount };

    [Fact]
    public void Charged_SumsTariffForAllMonths()
    {
        var payments = new[] { Month(1, 95m), Month(2, 95m), Month(3, 100.50m) };

        Assert.Equal(290.50m, Billing.Charged(payments));
    }

    [Fact]
    public void Paid_IgnoresUnpaidMonths()
    {
        var payments = new[] { Month(1, 95m, 95m), Month(2, 95m, null), Month(3, 95m, 50.25m) };

        Assert.Equal(145.25m, Billing.Paid(payments));
    }

    [Fact]
    public void Debt_IsChargedMinusPaid()
    {
        var payments = new[] { Month(1, 95m, 95m), Month(2, 95m, null), Month(3, 95m, null) };

        Assert.Equal(190m, Billing.Debt(payments));
    }

    [Fact]
    public void Debt_IsZeroWhenOverpaid()
    {
        var payments = new[] { Month(1, 95m, 200m), Month(2, 95m, null) };

        Assert.Equal(0m, Billing.Debt(payments));
    }

    [Fact]
    public void Debt_IsZeroWithoutPayments()
    {
        Assert.Equal(0m, Billing.Debt([]));
    }

    [Fact]
    public void Debt_KeepsKopecksExact()
    {
        // Деньги — decimal: 0,1 + 0,2 должно дать ровно 0,3, без ошибок округления double.
        var payments = new[] { Month(1, 0.1m, null), Month(2, 0.2m, null) };

        Assert.Equal(0.3m, Billing.Debt(payments));
    }

    [Fact]
    public void UnpaidMonths_CountsMonthsWithoutAmount()
    {
        var payments = new[] { Month(1, 95m, 95m), Month(2, 95m, null), Month(3, 95m, null), Month(4, 95m, 0m) };

        Assert.Equal(2, Billing.UnpaidMonths(payments));
    }

    [Fact]
    public void Due_SkipsCurrentAndFutureMonths()
    {
        // Как в старой программе: начисления на весь год заводятся заранее, а текущий месяц ещё не долг.
        var payments = Enumerable.Range(1, 12).Select(m => Month(m, 95m, m <= 6 ? 95m : null)).ToList();

        var due = Billing.Due(payments, new DateOnly(2026, 8, 31));
        Assert.Equal(95m, Billing.Debt(due));
        Assert.Equal(1, Billing.UnpaidMonths(due));

        Assert.Equal(190m, Billing.Debt(Billing.Due(payments, new DateOnly(2026, 9, 1))));
    }

    [Fact]
    public void IsOldPayer_TrueWhenNeverPaid()
    {
        Assert.True(Billing.IsOldPayer(null, new DateOnly(2026, 8, 31)));
    }

    [Theory]
    [InlineData("2023-08-31", true)]
    [InlineData("2023-08-30", true)]
    [InlineData("2023-09-01", false)]
    [InlineData("2026-08-01", false)]
    public void IsOldPayer_UsesThreeYearBoundary(string lastPayment, bool expected)
    {
        var today = new DateOnly(2026, 8, 31);

        Assert.Equal(expected, Billing.IsOldPayer(DateOnly.Parse(lastPayment), today));
    }

    [Theory]
    [InlineData(TicketKind.Antenna, "Антенна / СКПТ")]
    [InlineData(TicketKind.Cable, "Кабель / отвод")]
    [InlineData(TicketKind.Intercom, "Домофон")]
    [InlineData(TicketKind.Other, "Прочее")]
    public void KindTitle_ReturnsRussianTitle(TicketKind kind, string expected)
    {
        Assert.Equal(expected, Billing.KindTitle(kind));
    }

    [Fact]
    public void KindTitle_CoversEveryKind()
    {
        foreach (var kind in Enum.GetValues<TicketKind>())
            Assert.False(string.IsNullOrWhiteSpace(Billing.KindTitle(kind)));
    }

    [Theory]
    [InlineData(TicketStatus.Done, "да")]
    [InlineData(TicketStatus.Deferred, "отл.")]
    [InlineData(TicketStatus.Accepted, "")]
    public void StatusTitle_MatchesOldAversLabels(TicketStatus status, string expected)
    {
        Assert.Equal(expected, Billing.StatusTitle(status));
    }
}
