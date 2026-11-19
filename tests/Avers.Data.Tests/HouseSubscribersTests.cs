using Avers.Domain;

namespace Avers.Data.Tests;

public class HouseSubscribersTests
{
    [Fact]
    public void Load_ReadsHistoryOnlyForSelectedAccount()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var house = new House { Street = new Street { Name = "Учебная" }, Number = "1" };
        foreach (var account in new[] { DemoSeeder.StartAccount, "other" })
        {
            var subscriber = new Subscriber { House = house, Account = account };
            for (var year = 1998; year <= 2026; year++)
                subscriber.Payments.Add(new YearPayment { Year = year, Month = 1, Tariff = 150 });
            subscriber.Receipts.Add(new UnifiedReceipt { Moment = new DateOnly(2026, 1, 1), Paid = 150 });
            db.Subscribers.Add(subscriber);
        }
        db.SaveChanges();

        var loaded = HouseSubscribers.Load(db, house.Id);

        Assert.Equal(2, loaded.Subscribers.Count);
        Assert.Equal(29, loaded.Payments.Count);
        var selected = loaded.Subscribers.Single(s => s.Account == DemoSeeder.StartAccount);
        Assert.All(loaded.Payments, p => Assert.Equal(selected.Id, p.SubscriberId));
        Assert.Equal(selected.Id, Assert.Single(loaded.Receipts).SubscriberId);
        var other = loaded.Subscribers.Single(s => s.Account == "other");
        var changed = HouseSubscribers.Load(db, house.Id, other.Id);
        Assert.All(changed.Payments, p => Assert.Equal(other.Id, p.SubscriberId));
        Assert.Empty(HouseSubscribers.Load(db, house.Id, int.MaxValue).Payments);
    }
}
