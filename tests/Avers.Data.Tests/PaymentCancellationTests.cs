using Avers.Domain;

namespace Avers.Data.Tests;

public class PaymentCancellationTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cancel_ClearsMonth_RollsBackLastPayment_AndLogsOldValues()
    {
        using var database = new SqliteDatabase();
        int subscriberId, septemberId;
        using (var db = database.CreateContext())
        {
            var s = new Subscriber
            {
                House = new House { Street = new Street { Name = "Братская" }, Number = "10" },
                Account = "1",
                LastName = "Смирнов",
                LastPaymentDate = new DateOnly(2026, 9, 10)
            };
            s.Payments.Add(new YearPayment { Year = 2026, Month = 8, Tariff = 95, Amount = 95, PaidAt = new DateOnly(2026, 8, 5), Source = "Касса" });
            s.Payments.Add(new YearPayment { Year = 2026, Month = 9, Tariff = 95, Amount = 95, PaidAt = new DateOnly(2026, 9, 10), Source = "Касса", RecordedBy = "Орлов" });
            s.Payments.Add(new YearPayment { Year = 2026, Month = 10, Tariff = 95 });
            db.Subscribers.Add(s);
            db.SaveChanges();
            subscriberId = s.Id;
            septemberId = s.Payments.Single(p => p.Month == 9).Id;

            Assert.Null(PaymentCancellation.Cancel(db, subscriberId, septemberId, "Админ", At));
            Assert.Equal("Месяц 09.2026 не оплачен.", PaymentCancellation.Cancel(db, subscriberId, septemberId, "Админ", At));
            Assert.Equal("Месяц не найден.", PaymentCancellation.Cancel(db, subscriberId + 1, septemberId, "Админ", At));
        }

        using var check = database.CreateContext();
        var september = check.Payments.Single(p => p.Id == septemberId);
        Assert.Null(september.Amount);
        Assert.Null(september.PaidAt);
        Assert.Null(september.RecordedBy);
        Assert.Equal(new DateOnly(2026, 8, 5), check.Subscribers.Single().LastPaymentDate);
        var log = check.ChangeLog.Single();
        Assert.Equal("Отмена оплаты за 09.2026", log.Action);
        Assert.Contains("Внёс: Орлов → —", log.Changes);
    }
}
