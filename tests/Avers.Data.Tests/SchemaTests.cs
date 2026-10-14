using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data.Tests;

public class SchemaTests
{
    private static (Street Street, House House) AddHouse(AversDbContext db)
    {
        var street = new Street { Name = "Ленина" };
        var house = new House { Street = street, Number = "10" };
        db.Houses.Add(house);
        db.SaveChanges();
        return (street, house);
    }

    [Fact]
    public void Money_RoundTripsWithKopecks()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
        {
            var (_, house) = AddHouse(db);
            var sub = new Subscriber { House = house, LastName = "Иванова", Account = "100001", InnerCode = "1" };
            sub.Payments.Add(new YearPayment { Year = 2026, Month = 8, Tariff = 95m, Amount = 4914739.57m });
            db.Subscribers.Add(sub);
            db.SaveChanges();
        }

        using var check = database.CreateContext();
        var payment = check.Payments.Single();

        Assert.Equal(4914739.57m, payment.Amount);
        Assert.Equal(95m, payment.Tariff);
    }

    [Fact]
    public void Account_MustBeUnique()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var (_, house) = AddHouse(db);
        db.Subscribers.Add(new Subscriber { House = house, LastName = "Петров", Account = "200002", InnerCode = "2" });
        db.SaveChanges();

        db.Subscribers.Add(new Subscriber { House = house, LastName = "Сидоров", Account = "200002", InnerCode = "3" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Payment_OnePerSubscriberAndMonth()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var (_, house) = AddHouse(db);
        var sub = new Subscriber { House = house, LastName = "Петров", Account = "300003", InnerCode = "4" };
        sub.Payments.Add(new YearPayment { Year = 2026, Month = 1, Tariff = 95m });
        sub.Payments.Add(new YearPayment { Year = 2026, Month = 1, Tariff = 95m });
        db.Subscribers.Add(sub);

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void HouseNumber_MustBeUniqueOnStreet()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var (street, _) = AddHouse(db);

        db.Houses.Add(new House { StreetId = street.Id, Number = "10" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Cyrillic_IsStoredAsIs()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
        {
            db.Streets.Add(new Street { Name = "Красный Путь" });
            db.SaveChanges();
        }

        using var check = database.CreateContext();
        Assert.Equal("Красный Путь", check.Streets.Single().Name);
    }
}
