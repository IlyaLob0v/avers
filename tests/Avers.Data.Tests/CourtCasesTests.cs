using Avers.Domain;

namespace Avers.Data.Tests;

public class CourtCasesTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset At = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private static CourtCase Draft(int id = 0, string order = "2-1/2026") =>
        new() { Id = id, DelaAversId = "ДА-1", OrderNumber = order, OrderDate = new DateOnly(2026, 10, 1), Court = "с/у № 47", Amount = 475, Status = "Подано" };

    [Fact]
    public void Save_AddsThenEdits_DebtorsShowLatestCase()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var s = new Subscriber { House = new House { Street = new Street { Name = "Братская" }, Number = "10" }, Account = "1", LastName = "Смирнов" };
        s.Payments.Add(new YearPayment { Year = 2026, Month = 1, Tariff = 500 });
        db.Subscribers.Add(s);
        db.SaveChanges();

        Assert.Null(CourtCases.Save(db, s.Id, Draft(), "Юрист", At));
        Assert.Null(CourtCases.Save(db, s.Id, Draft(order: "2-2/2026"), "Юрист", At));
        var latest = db.CourtCases.OrderBy(c => c.Id).Last().Id;

        var edited = Draft(latest, "2-2/2026");
        edited.Status = "Выдан";
        Assert.Null(CourtCases.Save(db, s.Id, edited, "Юрист", At));

        Assert.Equal("Выдан", Debts.Load(db, Today).Debtors.Single().Case!.Status);
        Assert.Equal(["Новое судебное дело", "Новое судебное дело", "Судебное дело"], db.ChangeLog.OrderBy(e => e.Id).Select(e => e.Action));
    }

    [Fact]
    public void Debtors_ShowLimitationOfOldestUnpaidMonth()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var s = new Subscriber { House = new House { Street = new Street { Name = "Братская" }, Number = "10" }, Account = "1", LastName = "Смирнов" };
        s.Payments.Add(new YearPayment { Year = 2023, Month = 9, Tariff = 150, Amount = 150 });
        s.Payments.Add(new YearPayment { Year = 2023, Month = 10, Tariff = 150 });
        s.Payments.Add(new YearPayment { Year = 2023, Month = 11, Tariff = 150 });
        db.Subscribers.Add(s);
        db.SaveChanges();

        var d = Debts.Load(db, Today).Debtors.Single();

        Assert.Equal(new DateOnly(2023, 10, 1), d.OldestUnpaid);
        Assert.Equal(new DateOnly(2026, 11, 10), d.LimitationEnds);
    }

    [Fact]
    public void Save_RejectsEmptyNumbersAndForeignCase()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var house = new House { Street = new Street { Name = "Братская" }, Number = "10" };
        var a = new Subscriber { House = house, Account = "1", LastName = "А" };
        var b = new Subscriber { House = house, Account = "2", LastName = "Б" };
        db.Subscribers.AddRange(a, b);
        db.SaveChanges();
        Assert.Null(CourtCases.Save(db, a.Id, Draft(), "Юрист", At));
        var id = db.CourtCases.Single().Id;

        Assert.Equal("Укажите номер дела или номер судебного приказа.",
            CourtCases.Save(db, a.Id, new CourtCase { OrderDate = Today }, "Юрист", At));
        Assert.Equal("Дело не найдено.", CourtCases.Save(db, b.Id, Draft(id), "Юрист", At));
    }
}
