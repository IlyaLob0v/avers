using Avers.Domain;

namespace Avers.Data.Tests;

public class TariffsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private static (int House1, int Sub1, int Sub2, int Sub3) Seed(AversDbContext db)
    {
        var street = new Street { Name = "Братская" };
        var h1 = new House { Street = street, Number = "10" };
        var h2 = new House { Street = street, Number = "12" };
        Subscriber Sub(House h, string account)
        {
            var s = new Subscriber { House = h, Account = account, LastName = account };
            for (var m = 9; m <= 12; m++)
                s.Payments.Add(new YearPayment { Year = 2026, Month = m, Tariff = 95, Amount = m == 11 ? 95 : null });
            s.Payments.Add(new YearPayment { Year = 2027, Month = 1, Tariff = 95 });
            db.Subscribers.Add(s);
            return s;
        }
        var s1 = Sub(h1, "1");
        var s2 = Sub(h1, "2");
        var s3 = Sub(h2, "3");
        db.SaveChanges();
        return (h1.Id, s1.Id, s2.Id, s3.Id);
    }

    private static decimal[] TariffsOf(AversDbContext db, int subscriberId) =>
        db.Payments.Where(p => p.SubscriberId == subscriberId).OrderBy(p => p.Year).ThenBy(p => p.Month).Select(p => p.Tariff).ToArray();

    [Fact]
    public void Change_FromMonth_SkipsPaidMonths_ByScope()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var (house, s1, s2, s3) = Seed(db);

        Assert.Equal((null, 2), Tariffs.Change(db, 2026, 11, 110m, null, s1, "Админ", At));
        Assert.Equal((null, 4), Tariffs.Change(db, 2026, 12, 120m, house, null, "Админ", At));
        Assert.Equal((null, 3), Tariffs.Change(db, 2027, 1, 130m, null, null, "Админ", At));

        db.ChangeTracker.Clear();
        Assert.Equal([95m, 95m, 95m, 120m, 130m], TariffsOf(db, s1));
        Assert.Equal([95m, 95m, 95m, 120m, 130m], TariffsOf(db, s2));
        Assert.Equal([95m, 95m, 95m, 95m, 130m], TariffsOf(db, s3));
        Assert.Equal(3, db.ChangeLog.Count());
    }

    [Fact]
    public void Change_RejectsBadInput()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        Seed(db);

        Assert.NotNull(Tariffs.Change(db, 2026, 13, 100m, null, null, "Админ", At).Error);
        Assert.NotNull(Tariffs.Change(db, 2026, 10, 0m, null, null, "Админ", At).Error);
        Assert.Equal("Дом не найден.", Tariffs.Change(db, 2026, 10, 100m, 999, null, "Админ", At).Error);
        Assert.Empty(db.ChangeLog);
    }
}
