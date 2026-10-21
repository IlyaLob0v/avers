using Avers.Domain;

namespace Avers.Data.Tests;

public class ApartmentsTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset At = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private static int House(AversDbContext db)
    {
        var house = new House { Street = new Street { Name = "Братская" }, Number = "10" };
        db.Houses.Add(house);
        db.SaveChanges();
        return house.Id;
    }

    private static Subscriber Draft(int houseId, string account = "7001", string apartment = "15", DateOnly? signed = null) => new()
    {
        HouseId = houseId,
        Account = account,
        Apartment = apartment,
        LastName = " Смирнов ",
        FirstName = "Олег",
        ContractNumber = "ТО-2026/1",
        ContractDate = signed ?? new DateOnly(2026, 10, 5)
    };

    [Fact]
    public void Add_CreatesSubscriberWithMonthsFromContractToDecember()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
            Assert.Null(Apartments.Add(db, Draft(House(db)), 95m, Today, "Админ", At));

        using var check = database.CreateContext();
        var s = check.Subscribers.Single();
        Assert.Equal("Смирнов", s.LastName);
        Assert.Equal([10, 11, 12], check.Payments.Where(p => p.SubscriberId == s.Id && p.Year == 2026).Select(p => p.Month).Order());
        Assert.All(check.Payments, p => Assert.Equal(95m, p.Tariff));
        Assert.Equal("Новая квартира", check.ChangeLog.Single().Action);
    }

    [Fact]
    public void Add_OldContract_MonthsFromJanuary()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();

        Assert.Null(Apartments.Add(db, Draft(House(db), signed: new DateOnly(2019, 3, 1)), 95m, Today, "Админ", At));

        Assert.Equal(12, db.Payments.Count(p => p.Year == 2026));
    }

    [Fact]
    public void Add_RejectsDuplicatesAndBadInput()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var house = House(db);
        Assert.Null(Apartments.Add(db, Draft(house), 95m, Today, "Админ", At));

        Assert.Equal("Лицевой счёт 7001 уже есть.", Apartments.Add(db, Draft(house, apartment: "16"), 95m, Today, "Админ", At));
        Assert.StartsWith("Квартира 15 в этом доме уже есть", Apartments.Add(db, Draft(house, account: "7002"), 95m, Today, "Админ", At));
        Assert.NotNull(Apartments.Add(db, Draft(house, account: "7003", apartment: "17"), 0m, Today, "Админ", At));
        Assert.NotNull(Apartments.Add(db, Draft(house, account: "7004", apartment: "18", signed: new DateOnly(2027, 1, 1)), 95m, Today, "Админ", At));
        Assert.Equal("Дом не найден.", Apartments.Add(db, Draft(999, account: "7005"), 95m, Today, "Админ", At));
        Assert.Single(db.Subscribers);
    }
}
