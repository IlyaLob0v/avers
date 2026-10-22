using Avers.Domain;

namespace Avers.Data.Tests;

/// <summary>Операции, которые одинаково выполняют сервер и учебная база.</summary>
public class SharedOperationsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private static Subscriber AddSubscriber(AversDbContext db)
    {
        var s = new Subscriber
        {
            House = new House { Street = new Street { Name = "Братская" }, Number = "10" },
            Account = "1",
            LastName = "Смирнов",
            Section = "2",
            Apartment = "15",
            Phone = "555-01"
        };
        s.Payments.Add(new YearPayment { Year = 2026, Month = 1, Tariff = 95, Amount = 95, PaidAt = new DateOnly(2026, 1, 5) });
        s.Payments.Add(new YearPayment { Year = 2026, Month = 2, Tariff = 95 });
        db.Subscribers.Add(s);
        db.SaveChanges();
        return s;
    }

    [Fact]
    public void RecordPayment_PaysFirstUnpaidMonthByTariff_ThenReportsYearPaid()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var s = AddSubscriber(db);
        var today = new DateOnly(2026, 10, 22);

        var (paid, error) = Payments.Record(db, s, 2026, null, null, today, " ", null, "Орлова", "Орлова", At);
        var (again, yearPaid) = Payments.Record(db, s, 2026, null, null, today, null, null, "Орлова", "Орлова", At);

        Assert.Null(error);
        Assert.Equal((2, 95m, "Касса"), (paid!.Month, paid.Amount!.Value, paid.Source!));
        Assert.Equal(today, s.LastPaymentDate);
        Assert.Null(again);
        Assert.Equal("Все месяцы 2026 уже оплачены.", yearPaid);
        Assert.Equal("Месяц 01.2026 уже оплачен.", Payments.Record(db, s, 2026, 1, 10, today, null, null, "Орлова", "Орлова", At).Error);
    }

    [Fact]
    public void SaveHousePassport_TrimsFields_AndReportsMissingHouse()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var houseId = AddSubscriber(db).HouseId;

        Assert.True(HousePassport.Save(db, houseId, new House { Antennas = "  2 антенны ", TechFloorKey = "у вахтёра " }, "Орлова", At));
        Assert.False(HousePassport.Save(db, houseId + 1, new House(), "Орлова", At));

        var house = db.Houses.Single(h => h.Id == houseId);
        Assert.Equal(("2 антенны", "у вахтёра"), (house.Antennas, house.TechFloorKey));
    }

    [Fact]
    public void AcceptTicket_NumbersInOrder_AndFillsBlanksFromSubscriber()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var s = AddSubscriber(db);

        var first = Tickets.Accept(db, new WorkTicket { HouseId = s.HouseId, Fault = " Нет сигнала ", Kind = TicketKind.Antenna });
        var second = Tickets.Accept(db, new WorkTicket { HouseId = s.HouseId, SubscriberId = s.Id, Apartment = " 16 ", Fault = "Помехи" });

        Assert.Equal((1, "Нет сигнала", TicketStatus.Accepted), (first.Number, first.Fault, first.Status));
        Assert.Equal((2, "2", "16", "555-01"), (second.Number, second.Section, second.Apartment, second.Phone));
    }
}
