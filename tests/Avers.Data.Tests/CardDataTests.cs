using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data.Tests;

public class CardDataTests
{
    [Fact]
    public void PaymentWarningMonths_DefaultsToTwo_AndIsStored()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
        {
            Assert.Equal(PaymentAge.DefaultWarningMonths, Settings.PaymentWarningMonths(db));
            Settings.SetPaymentWarningMonths(db, 4, "Админ", DateTimeOffset.UnixEpoch);
            Settings.SetPaymentWarningMonths(db, 3, "Админ", DateTimeOffset.UnixEpoch);
        }

        using var check = database.CreateContext();
        Assert.Equal(3, Settings.PaymentWarningMonths(check));
        Assert.Single(check.Settings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    public void PaymentWarningMonths_OutOfRange_Throws(int months)
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();

        Assert.Throws<ArgumentOutOfRangeException>(() => Settings.SetPaymentWarningMonths(db, months, "Админ", DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void PaymentWarningMonths_GarbageInDatabase_FallsBackToDefault()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        db.Settings.Add(new AppSetting { Key = AppSetting.PaymentWarningMonths, Value = "много" });
        db.SaveChanges();

        Assert.Equal(PaymentAge.DefaultWarningMonths, Settings.PaymentWarningMonths(db));
    }

    [Fact]
    public void ContractAndPaymentAuthor_RoundTrip()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
        {
            var house = new House { Street = new Street { Name = "Ленина" }, Number = "10" };
            var sub = new Subscriber
            {
                House = house,
                LastName = "Белоусов",
                Account = "1",
                InnerCode = "1",
                ContractNumber = "ТО-2009/1",
                ContractHolder = "Белоусова Галина Ивановна"
            };
            sub.Payments.Add(new YearPayment
            {
                Year = 2026,
                Month = 8,
                Tariff = 95,
                Amount = 95,
                PaidAt = new(2026, 8, 3),
                RecordedBy = "Орлов С.А.",
                PayerName = "Белоусова Г.И."
            });
            db.Subscribers.Add(sub);
            db.SaveChanges();
        }

        using var check = database.CreateContext();
        var s = check.Subscribers.Include(x => x.Payments).Single();
        Assert.Equal("ТО-2009/1", s.ContractNumber);
        Assert.Equal("Белоусова Галина Ивановна", s.ContractParty);
        Assert.Equal("Орлов С.А.", s.Payments.Single().RecordedBy);
        Assert.Equal("Белоусова Г.И.", s.Payments.Single().PayerName);
    }
}
