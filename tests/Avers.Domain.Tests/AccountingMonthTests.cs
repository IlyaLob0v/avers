using Avers.Domain;

namespace Avers.Domain.Tests;

public class AccountingMonthTests
{
    private static YearPayment Row(int month, decimal tariff, decimal? amount = null, DateOnly? paidAt = null, string? source = null) =>
        new() { Year = 2026, Month = month, Tariff = tariff, Amount = amount, PaidAt = paidAt, Source = source };

    [Fact]
    public void Shipment_CountsMonthRowsByTariff()
    {
        var rows = new[] { Row(8, 150m), Row(8, 150m, 150m), Row(8, 200m), Row(9, 150m), Row(8, 0m) };

        var lines = AccountingMonth.Shipment(rows, 2026, 8);

        Assert.Equal([new(150m, 2, 300m), new(200m, 1, 200m)], lines);
    }

    [Fact]
    public void Receipts_GroupByPaymentDateNotByBilledMonth()
    {
        var rows = new[]
        {
            Row(7, 150m, 150m, new DateOnly(2026, 8, 3), "ОЭК"),
            Row(8, 150m, 150m, new DateOnly(2026, 8, 20), "ОЭК"),
            Row(8, 150m, 100m, new DateOnly(2026, 8, 21), "Касса"),
            Row(8, 150m, 150m, new DateOnly(2026, 9, 1), "ОЭК"),
            Row(8, 150m, 50m, new DateOnly(2026, 8, 2), " "),
            Row(8, 150m)
        };

        var lines = AccountingMonth.Receipts(rows, 2026, 8);

        Assert.Equivalent(new[] { new AccountingMonth.ReceiptLine("Касса", 1, 100m), new("ОЭК", 2, 300m), new("не указан", 1, 50m) }, lines, strict: true);
    }
}
