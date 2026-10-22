using Avers.Domain;

namespace Avers.Data.Tests;

public class DebtsTests
{
    [Fact]
    public void Load_SkipsCurrentMonth()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var s = new Subscriber { House = new House { Street = new Street { Name = "Братская" }, Number = "10" }, Account = "1", LastName = "Смирнов" };
        for (var month = 7; month <= 12; month++)
            s.Payments.Add(new YearPayment { Year = 2026, Month = month, Tariff = 150 });
        db.Subscribers.Add(s);
        db.SaveChanges();

        var debtor = Debts.Load(db, new DateOnly(2026, 10, 21)).Debtors.Single();

        Assert.Equal(450m, debtor.Debt);
        Assert.Equal(3, debtor.Unpaid);
    }
}
