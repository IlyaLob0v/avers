using Avers.Domain;

namespace Avers.Data.Tests;

public class DebtsTests
{
    [Fact]
    public async Task LoadAsync_CanBeCancelledWithoutReturningPartialData()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Debts.LoadAsync(db, new DateOnly(2026, 10, 21), cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void Load_IsBoundedAndPreservesTotalCount()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var house = new House { Street = new Street { Name = "Учебная" }, Number = "1" };
        for (var i = 0; i < 125; i++)
            db.Subscribers.Add(new Subscriber { House = house, Account = i.ToString(), LastName = "Должник" });
        db.SaveChanges();

        var first = Debts.Load(db, new DateOnly(2026, 10, 21));
        var last = Debts.Load(db, new DateOnly(2026, 10, 21), offset: 100);
        Assert.Equal(100, first.Debtors.Count);
        Assert.Equal(125, first.TotalCount);
        Assert.Equal(25, last.Debtors.Count);
        Assert.Empty(first.Debtors.Select(d => d.SubscriberId).Intersect(last.Debtors.Select(d => d.SubscriberId)));
        Assert.Equal(125, Debts.Load(db, new DateOnly(2026, 10, 21), all: true).Debtors.Count);
    }

    [Fact]
    public void FiltersAndHouseTotals_KeepCreditAndCurrentMonthRules()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var house = new House { Street = new Street { Name = "Учебная" }, Number = "19Д" };
        var debtor = new Subscriber { House = house, Account = "debt", Apartment = "12", IsLegalEntity = true };
        debtor.Payments.Add(new YearPayment { Year = 2026, Month = 9, Tariff = 600, Amount = 100 });
        debtor.Payments.Add(new YearPayment { Year = 2026, Month = 10, Tariff = 9000 });
        var credit = new Subscriber { House = house, Account = "credit" };
        credit.Payments.Add(new YearPayment { Year = 2026, Month = 9, Tariff = 100, Amount = 200 });
        db.Subscribers.AddRange(debtor, credit);
        db.SaveChanges();
        var today = new DateOnly(2026, 10, 21);

        var filtered = Debts.Load(db, today, new(DebtFrom: 450, Address: "учебная 19д", IsLegalEntity: true));
        Assert.Equal(500, Assert.Single(filtered.Debtors).Debt);
        Assert.Empty(Debts.Load(db, today, new(HasCase: true)).Debtors);
        Assert.Equal(0, Debts.Load(db, today, new(IsLegalEntity: false)).Debtors.Single().Debt);
        Assert.Equal(500, Debts.LoadHouseTotals(db, today)[house.Id]);
    }

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
