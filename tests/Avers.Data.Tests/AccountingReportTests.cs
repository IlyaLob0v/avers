using Avers.Data;
using Avers.Domain;

namespace Avers.Data.Tests;

public sealed class AccountingReportTests
{
    [Fact]
    public void Load_SeparatesImportedPaymentsByDateAndSource_AndIncludesNewPayments()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var subscriber = new Subscriber
        {
            Account = "1",
            LastName = "Тестов",
            House = new House { Street = new Street { Name = "Учебная" }, Number = "1" }
        };
        subscriber.Payments.Add(new YearPayment
        {
            Year = 2026,
            Month = 1,
            Tariff = 150,
            Amount = 150,
            ImportedFromJack = true,
            PaidAt = new DateOnly(2026, 2, 2),
            Source = "Почта"
        });
        subscriber.Payments.Add(new YearPayment
        {
            Year = 2026,
            Month = 2,
            Tariff = 60,
            Amount = 60,
            PaidAt = new DateOnly(2026, 2, 5),
            Source = "Касса"
        });
        db.Subscribers.Add(subscriber);
        db.SaveChanges();
        db.JackPayments.AddRange(
            new JackPayment
            {
                Id = 1,
                SubscriberId = subscriber.Id,
                Period = new DateTime(2026, 1, 1),
                PaidAt = new DateTime(2026, 1, 20),
                Amount = 100,
                Source = "Касса"
            },
            new JackPayment
            {
                Id = 2,
                SubscriberId = subscriber.Id,
                Period = new DateTime(2026, 1, 1),
                PaidAt = new DateTime(2026, 2, 2),
                Amount = 50,
                Source = "Почта"
            });
        db.SaveChanges();

        var january = AccountingReport.Load(db, 2026, 1);
        var february = AccountingReport.Load(db, 2026, 2);

        Assert.Equal(new AccountingMonth.ShipmentLine(150, 1, 150), Assert.Single(january.Shipment));
        Assert.Equal(new AccountingMonth.ReceiptLine("Касса", 1, 100), Assert.Single(january.Receipts));
        Assert.Equal(new AccountingMonth.ReceiptLine("Касса", 1, 60), february.Receipts.Single(r => r.Source == "Касса"));
        Assert.Equal(new AccountingMonth.ReceiptLine("Почта", 1, 50), february.Receipts.Single(r => r.Source == "Почта"));
    }
}
