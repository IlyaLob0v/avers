using System.Data.Common;
using Avers.Domain;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Avers.Data.Tests;

public class OekImportTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Три квартиры с августом 2026 по 150 ₽; у второй август уже оплачен в кассе.</summary>
    private static void Seed(AversDbContext db)
    {
        var house = new House { Street = new Street { Name = "Мира" }, Number = "100Б" };
        foreach (var account in new[] { "1", "2", "3" })
        {
            var s = new Subscriber { House = house, Account = account, InnerCode = account, LastName = "Абонент" + account };
            s.Payments.Add(new YearPayment { Year = 2026, Month = 8, Tariff = 150, Amount = account == "2" ? 150 : null, Source = account == "2" ? "Касса" : null });
            db.Subscribers.Add(s);
        }
        db.SaveChanges();
    }

    private static OekImportRequest August(params OekRow[] rows) => new(2026, 8, [.. rows]);

    [Fact]
    public void Payment_GoesToFileMonth_AndOekAccountIsRemembered()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        Seed(db);

        var result = OekImport.Apply(db, August(new OekRow("83072480817", "1", "МИРА 100Б", "40", 150, 150, new DateOnly(2026, 8, 14))), "Орлов", At);

        Assert.Equal((1, 1, 150m), (result.Matched, result.Payments, result.PaidTotal));
        Assert.Empty(result.Mismatches);
        var s = db.Subscribers.Single(x => x.Account == "1");
        Assert.Equal("83072480817", s.OekAccount);
        Assert.Equal(new DateOnly(2026, 8, 14), s.LastPaymentDate);
        var august = db.Payments.Single(p => p.SubscriberId == s.Id);
        Assert.Equal((150m, "ОЭК", new DateOnly(2026, 8, 14)), (august.Amount!.Value, august.Source!, august.PaidAt!.Value));
    }

    [Fact]
    public void SameFileTwice_LoadsOnce()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        Seed(db);
        var file = August(new OekRow("83072480817", "1", "", "", 150, 150, new DateOnly(2026, 8, 14)));

        OekImport.Apply(db, file, "Орлов", At);
        var again = OekImport.Apply(db, file, "Орлов", At);

        Assert.Equal((0, 1), (again.Payments, again.AlreadyLoaded));
        Assert.Equal(150m, db.Payments.Single(p => p.Subscriber.Account == "1").Amount);
    }

    [Fact]
    public void WithoutOurAccount_FoundByOekAccount()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        Seed(db);
        OekImport.Apply(db, August(new OekRow("84132427744", "3", "", "", 150, 0, null)), "Орлов", At);

        var result = OekImport.Apply(db, August(new OekRow("84132427744", "", "", "", 150, 150, new DateOnly(2026, 8, 10))), "Орлов", At);

        Assert.Equal(1, result.Payments);
        Assert.Equal(150m, db.Payments.Single(p => p.Subscriber.Account == "3").Amount);
    }

    [Fact]
    public void Mismatches_AreListed_AndPaidMonthIsNotTouched()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        Seed(db);

        var result = OekImport.Apply(db, August(
            new OekRow("1", "2", "", "", 150, 150, new DateOnly(2026, 8, 3)),
            new OekRow("2", "3", "", "", 300, 0, null),
            new OekRow("3", "999", "", "", 150, 150, null)), "Орлов", At);

        Assert.Equal(0, result.Payments);
        Assert.Collection(result.Mismatches,
            m => Assert.StartsWith("месяц уже оплачен в Аверсе", m.Problem),
            m => Assert.Equal(("начисление расходится", 150m), (m.Problem, m.AversTariff!.Value)),
            m => Assert.Equal("нет в Аверсе", m.Problem));
        var paid = db.Payments.Single(p => p.Subscriber.Account == "2");
        Assert.Equal("Касса", paid.Source);
        Assert.Single(db.ChangeLog);
    }

    [Fact]
    public void CashierPaysDuringImport_CashierPaymentIsKept()
    {
        using var database = new SqliteDatabase();
        using (var seed = database.CreateContext())
            Seed(seed);
        // Кассир вносит август квартиры 1 после того, как загрузка прочитала месяцы, но до её сохранения.
        var cashier = new BeforeWrite(() =>
        {
            using var other = database.CreateContext();
            var s = other.Subscribers.Single(x => x.Account == "1");
            var (_, error) = Payments.Record(other, s, 2026, 8, 150, new DateOnly(2026, 9, 20), "Касса", null, "Кассир", "Кассир", At);
            Assert.Null(error);
        });
        using var db = database.CreateContext(cashier);

        var result = OekImport.Apply(db, August(new OekRow("83072480817", "1", "", "", 150, 150, new DateOnly(2026, 8, 14))), "Орлов", At);

        Assert.Equal(0, result.Payments);
        Assert.StartsWith("месяц уже оплачен в Аверсе", Assert.Single(result.Mismatches).Problem);
        using var check = database.CreateContext();
        var august = check.Payments.Single(p => p.Subscriber.Account == "1");
        Assert.Equal(("Касса", "Кассир", new DateOnly(2026, 9, 20)), (august.Source!, august.RecordedBy!, august.PaidAt!.Value));
    }

    /// <summary>Один раз выполняет действие перед первой транзакцией контекста — между чтением и записью.</summary>
    private sealed class BeforeWrite(Action action) : DbTransactionInterceptor
    {
        private Action? _action = action;

        public override InterceptionResult<DbTransaction> TransactionStarting(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result)
        {
            var run = _action;
            _action = null;
            run?.Invoke();
            return result;
        }
    }
}
