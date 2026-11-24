using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data.Tests;

public class ConcurrentEditingTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateTimeOffset At = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private static int Seed(AversDbContext db)
    {
        var subscriber = new Subscriber
        {
            Account = "1",
            LastName = "Тест",
            House = new House { Number = "1", Street = new Street { Name = "Тестовая" } },
            Payments = [new YearPayment { Year = 2026, Month = 12, Tariff = 100, Amount = 100, PaidAt = Today }]
        };
        db.Subscribers.Add(subscriber);
        db.SaveChanges();
        return subscriber.Id;
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(10_000_000_000)]
    public void InvalidAmount_DoesNotChangePayment(decimal amount)
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var id = Seed(db);
        PaymentCancellation.Cancel(db, id, db.Payments.Single().Id, "Админ", At);
        var result = Payments.Record(db, db.Subscribers.Single(), 2026, 12, amount, Today, null, null, "Кассир", "Кассир", At);
        Assert.Null(result.Payment);
        Assert.NotNull(result.Error);
        db.ChangeTracker.Clear();
        Assert.Null(db.Payments.Single().Amount);
    }

    [Fact]
    public void ExcessiveTariff_IsRejectedBeforeChangingAccruals()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var id = Seed(db);
        PaymentCancellation.Cancel(db, id, db.Payments.Single().Id, "Админ", At);
        Assert.NotNull(Tariffs.Change(db, 2026, 12, 10_000_000_000m, null, null, "Админ", At).Error);
        Assert.Equal(100, db.Payments.AsNoTracking().Single().Tariff);
    }

    [Fact]
    public void ExistingSqliteSchema_GainsRevisions_WithoutLosingData()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        Seed(db);
        db.ChangeTracker.Clear();
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Houses\" DROP COLUMN \"Revision\"");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Organizations\" DROP COLUMN \"Revision\"");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Payments\" DROP COLUMN \"Revision\"");
        SqliteSchema.EnsureRevisions(db);
        SqliteSchema.EnsureRevisions(db);
        Assert.Equal(100, db.Payments.Single().Amount);
        Assert.Equal("Тест", db.Subscribers.Single().LastName);
        Assert.Equal(0, db.Payments.Single().Revision);
        Assert.Equal(0, db.Houses.Single().Revision);
    }

    [Fact]
    public void StaleCancellation_CannotEraseReplacementPayment()
    {
        using var database = new SqliteDatabase();
        using var stale = database.CreateContext();
        var id = Seed(stale);
        var old = stale.Payments.Single();
        using (var colleague = database.CreateContext())
        {
            Assert.Null(PaymentCancellation.Cancel(colleague, id, old.Id, "Коллега", At));
            Assert.NotNull(Payments.Record(colleague, colleague.Subscribers.Single(), 2026, 12, 120, Today, null, null, "Кассир", "Кассир", At).Payment);
        }
        Assert.Throws<DbUpdateConcurrencyException>(() => PaymentCancellation.Cancel(stale, id, old.Id, "Админ", At));
        using var check = database.CreateContext();
        Assert.Equal(120, check.Payments.Single().Amount);
        Assert.Equal(2, check.ChangeLog.Count());
    }

    [Fact]
    public void StaleTermination_CannotDeletePaidMonth_OrSaveHalfOfContract()
    {
        using var database = new SqliteDatabase();
        using var stale = database.CreateContext();
        var id = Seed(stale);
        Assert.Null(PaymentCancellation.Cancel(stale, id, stale.Payments.Single().Id, "Админ", At));
        using (var cashier = database.CreateContext())
            Assert.NotNull(Payments.Record(cashier, cashier.Subscribers.Single(), 2026, 12, 120, Today, null, null, "Кассир", "Кассир", At).Payment);
        Assert.Throws<DbUpdateConcurrencyException>(() => SubscriberContracts.Save(stale, stale.Subscribers.Single(),
            new Subscriber { ContractTerminated = Today }, Today, "Админ", At));
        using var check = database.CreateContext();
        Assert.Equal(120, check.Payments.Single().Amount);
        Assert.Null(check.Subscribers.Single().ContractTerminated);
    }
}
