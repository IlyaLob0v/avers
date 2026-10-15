using Avers.Domain;

namespace Avers.Domain.Tests;

public class PaymentAgeTests
{
    private static readonly DateOnly Today = new(2026, 8, 31);

    [Theory]
    [InlineData("2026-07-09", 1)] // Иванова в учебной базе
    [InlineData("2026-05-22", 3)] // Петров
    [InlineData("2026-08-31", 0)]
    [InlineData("2026-08-01", 0)]
    [InlineData("2026-07-31", 1)]
    [InlineData("2026-06-30", 2)]
    [InlineData("2022-06-14", 50)]
    public void MonthsSince_CountsFullCalendarMonths(string last, int expected)
    {
        Assert.Equal(expected, PaymentAge.MonthsSince(DateOnly.Parse(last), Today));
    }

    [Fact]
    public void MonthsSince_NoPayment_IsNull()
    {
        Assert.Null(PaymentAge.MonthsSince(null, Today));
    }

    [Fact]
    public void MonthsSince_FutureDate_IsZero()
    {
        Assert.Equal(0, PaymentAge.MonthsSince(Today.AddDays(5), Today));
    }

    [Fact]
    public void MonthsSince_FullMonthBoundary()
    {
        Assert.Equal(1, PaymentAge.MonthsSince(new DateOnly(2026, 7, 9), new DateOnly(2026, 9, 8)));
        Assert.Equal(2, PaymentAge.MonthsSince(new DateOnly(2026, 7, 9), new DateOnly(2026, 9, 9)));
    }

    [Fact]
    public void IsLate_ExactlyAtThreshold_IsLate()
    {
        Assert.False(PaymentAge.IsLate(new DateOnly(2026, 7, 1), Today));
        Assert.True(PaymentAge.IsLate(new DateOnly(2026, 6, 30), Today));
    }

    [Fact]
    public void IsLate_UsesConfiguredThreshold()
    {
        var last = new DateOnly(2026, 5, 22);

        Assert.True(PaymentAge.IsLate(last, Today, 3));
        Assert.False(PaymentAge.IsLate(last, Today, 4));
    }

    [Fact]
    public void IsLate_NeverPaid_IsLate()
    {
        Assert.True(PaymentAge.IsLate(null, Today));
    }

    [Theory]
    [InlineData(1, "1 месяц")]
    [InlineData(2, "2 месяца")]
    [InlineData(5, "5 месяцев")]
    [InlineData(11, "11 месяцев")]
    [InlineData(21, "21 месяц")]
    [InlineData(24, "24 месяца")]
    public void MonthsText_RussianPlural(int months, string expected)
    {
        Assert.Equal(expected, PaymentAge.MonthsText(months));
    }

    [Fact]
    public void LastPayment_TakesLatestPaidDate_IgnoresUnpaid()
    {
        var payments = new[]
        {
            new YearPayment { Year = 2026, Month = 6, Amount = 95, PaidAt = new(2026, 6, 10) },
            new YearPayment { Year = 2026, Month = 7, Amount = 95, PaidAt = new(2026, 7, 9), RecordedBy = "Орлов С.А." },
            new YearPayment { Year = 2026, Month = 8, Amount = null, PaidAt = null }
        };

        var last = PaymentAge.LastPayment(payments);

        Assert.Equal(7, last!.Month);
        Assert.Equal("Орлов С.А.", last.RecordedBy);
    }

    [Fact]
    public void LastPayment_SameDayForTwoMonths_TakesLaterMonth()
    {
        var day = new DateOnly(2026, 8, 3);
        var payments = new[]
        {
            new YearPayment { Year = 2026, Month = 8, Amount = 95, PaidAt = day },
            new YearPayment { Year = 2026, Month = 9, Amount = 95, PaidAt = day }
        };

        Assert.Equal(9, PaymentAge.LastPayment(payments)!.Month);
    }

    [Fact]
    public void LastPayment_None_IsNull()
    {
        Assert.Null(PaymentAge.LastPayment([]));
    }
}
