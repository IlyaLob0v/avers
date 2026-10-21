using Avers.Domain;

namespace Avers.Data.Tests;

public class AccrualsTests
{
    private static readonly DateTimeOffset At = new(2026, 12, 20, 10, 0, 0, TimeSpan.Zero);

    private static Subscriber Add(AversDbContext db, House house, string account, DateOnly? terminated, params (int Year, int Month, decimal Tariff)[] months)
    {
        var s = new Subscriber { House = house, Account = account, LastName = account, ContractTerminated = terminated };
        foreach (var (year, month, tariff) in months)
            s.Payments.Add(new YearPayment { Year = year, Month = month, Tariff = tariff });
        db.Subscribers.Add(s);
        return s;
    }

    [Fact]
    public void OpenYear_CreatesTwelveMonthsWithLastTariff_OnlyForActiveSubscribersWithoutTheYear()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
        {
            var house = new House { Street = new Street { Name = "Братская" }, Number = "10" };
            Add(db, house, "1", null, (2026, 11, 95), (2026, 12, 100));
            Add(db, house, "2", new DateOnly(2026, 6, 30), (2026, 6, 95));
            Add(db, house, "4", null, (2027, 5, 110));
            Add(db, house, "5", null);
            db.SaveChanges();

            Assert.Equal(1, Accruals.OpenYear(db, 2027, "Админ", At));
            Assert.Equal(0, Accruals.OpenYear(db, 2027, "Админ", At));
        }

        using var check = database.CreateContext();
        var opened = check.Payments.Where(p => p.Year == 2027).ToList()
            .GroupBy(p => check.Subscribers.Single(s => s.Id == p.SubscriberId).Account)
            .ToDictionary(g => g.Key, g => g.ToList());
        Assert.Equal(["1", "4"], opened.Keys.Order());
        Assert.Equal(Enumerable.Range(1, 12), opened["1"].Select(p => p.Month).Order());
        Assert.All(opened["1"], p => Assert.Equal(100m, p.Tariff));
        Assert.All(opened["1"], p => Assert.Null(p.Amount));
        Assert.Single(opened["4"]);
        Assert.Equal("Открыто абонентов: 1", check.ChangeLog.Single().Changes);
    }
}
