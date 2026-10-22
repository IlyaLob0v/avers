using Avers.Domain;

namespace Avers.Data.Tests;

public class SubscriberContractsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);

    /// <summary>Договор с 2025 года, 2026 год открыт по 95 ₽; оплачены февраль и (вперёд) ноябрь.</summary>
    private static int Seed(AversDbContext db)
    {
        var s = new Subscriber
        {
            House = new House { Street = new Street { Name = "Братская" }, Number = "10" },
            Account = "1",
            LastName = "Смирнов",
            ContractDate = new DateOnly(2025, 1, 15)
        };
        for (var m = 1; m <= 12; m++)
            s.Payments.Add(new YearPayment { Year = 2026, Month = m, Tariff = 95, Amount = m is 2 or 11 ? 95 : null });
        db.Subscribers.Add(s);
        db.SaveChanges();
        return s.Id;
    }

    private static void Save(AversDbContext db, int id, DateOnly? signed, DateOnly? ended)
    {
        db.ChangeTracker.Clear();
        var s = db.Subscribers.Single(x => x.Id == id);
        SubscriberContracts.Save(db, s, new Subscriber { ContractDate = signed, ContractTerminated = ended }, Today, "Админ", At);
        db.ChangeTracker.Clear();
    }

    private static int[] Months(AversDbContext db, int id) =>
        db.Payments.Where(p => p.SubscriberId == id).OrderBy(p => p.Year).ThenBy(p => p.Month).Select(p => p.Month).ToArray();

    [Fact]
    public void Terminate_RemovesUnpaidMonthsAfterIt_KeepsPaid()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var id = Seed(db);

        Save(db, id, new DateOnly(2025, 1, 15), new DateOnly(2026, 3, 20));

        Assert.Equal([1, 2, 3, 11], Months(db, id));
        Assert.Equal(new DateOnly(2026, 3, 20), db.Subscribers.Single().ContractTerminated);
        Assert.Contains("Снято неоплаченных месяцев: 8", db.ChangeLog.Single().Changes);
    }

    [Fact]
    public void Untermination_RestoresMonths_FromNewContractDate()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var id = Seed(db);
        Save(db, id, new DateOnly(2025, 1, 15), new DateOnly(2026, 3, 20));

        Save(db, id, new DateOnly(2025, 1, 15), null);
        Assert.Equal(Enumerable.Range(1, 12), Months(db, id));
        Assert.All(db.Payments, p => Assert.Equal(95m, p.Tariff));

        Save(db, id, new DateOnly(2025, 1, 15), new DateOnly(2026, 3, 20));
        Save(db, id, new DateOnly(2026, 9, 5), null);
        Assert.Equal([1, 2, 3, 9, 10, 11, 12], Months(db, id));
    }
}
