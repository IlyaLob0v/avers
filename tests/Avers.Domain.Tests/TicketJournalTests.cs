using Avers.Domain;

namespace Avers.Domain.Tests;

public class TicketJournalTests
{
    private static readonly Subscriber Ivanova = new() { Id = 1, LastName = "Иванова" };

    private static WorkTicket T(int number, DateOnly date, int houseId = 1, TicketKind kind = TicketKind.Antenna,
        TicketStatus status = TicketStatus.Accepted, string apartment = "68", string fault = "Нет сигнала") => new()
        {
            Number = number,
            Date = date,
            HouseId = houseId,
            Kind = kind,
            Status = status,
            Apartment = apartment,
            Fault = fault,
            SubscriberId = apartment == "68" ? Ivanova.Id : null,
            Subscriber = apartment == "68" ? Ivanova : null
        };

    private static readonly List<WorkTicket> Tickets =
    [
        T(1, new(2025, 12, 30)),
        T(2, new(2026, 1, 5), status: TicketStatus.Done),
        T(3, new(2026, 1, 20), houseId: 2, kind: TicketKind.Cable, apartment: "7"),
        T(4, new(2026, 8, 24), kind: TicketKind.Amplifier, status: TicketStatus.Done, fault: "Пропадание сигнала вечером"),
        T(5, new(2026, 8, 31), status: TicketStatus.Deferred, apartment: "41")
    ];

    private static int[] Numbers(TicketFilter f) => Tickets.Apply(f).Select(t => t.Number).Order().ToArray();

    [Fact]
    public void Period_IncludesBothEnds()
    {
        Assert.Equal([2, 3], Numbers(new TicketFilter { From = new(2026, 1, 5), To = new(2026, 1, 20) }));
    }

    [Fact]
    public void ForYear_TakesWholeYearOnly()
    {
        Assert.Equal([2, 3, 4, 5], Numbers(TicketFilter.ForYear(2026)));
        Assert.Equal([1], Numbers(TicketFilter.ForYear(2025)));
    }

    [Fact]
    public void ForMonth_EndsOnLastDayOfMonth()
    {
        var f = TicketFilter.ForMonth(2026, 2);

        Assert.Equal(new DateOnly(2026, 2, 28), f.To);
        Assert.Equal([4, 5], Numbers(TicketFilter.ForMonth(2026, 8)));
    }

    [Fact]
    public void HouseAndKind_FindAntennaTicketsOfHouse()
    {
        Assert.Equal([1, 2, 5], Numbers(new TicketFilter { HouseId = 1, Kind = TicketKind.Antenna }));
    }

    [Fact]
    public void Apartment_And_Subscriber()
    {
        Assert.Equal([5], Numbers(new TicketFilter { HouseId = 1, Apartment = " 41 " }));
        Assert.Equal([1, 2, 4], Numbers(new TicketFilter { SubscriberId = 1 }));
    }

    [Fact]
    public void OnlyOpen_HidesDone()
    {
        Assert.Equal([1, 3, 5], Numbers(new TicketFilter { OnlyOpen = true }));
    }

    [Theory]
    [InlineData("ВЕЧЕР", new[] { 4 })]
    [InlineData("иван", new[] { 1, 2, 4 })]
    [InlineData("3", new[] { 3 })]
    public void Text_SearchesFaultSurnameAndNumber(string text, int[] expected)
    {
        Assert.Equal(expected, Numbers(new TicketFilter { Text = text }));
    }

    [Fact]
    public void ByMonth_AlwaysTwelveMonths_CountsAcceptedAndDone()
    {
        var stats = TicketJournal.ByMonth(Tickets, 2026);

        Assert.Equal(12, stats.Count);
        Assert.Equal(new TicketMonthStat(1, 2, 1, 1), stats[0]);
        Assert.Equal(new TicketMonthStat(8, 2, 1, 1), stats[7]);
        Assert.Equal(0, stats[5].Accepted);
        Assert.Equal(4, stats.Sum(s => s.Accepted));
    }

    [Fact]
    public void Years_IncludeCurrentYearEvenWithoutTickets()
    {
        Assert.Equal([2027, 2026, 2025], TicketJournal.Years(Tickets, new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void Newest_SortsByDateThenTime()
    {
        var a = new WorkTicket { Number = 1, Date = new(2026, 8, 31), Time = new(8, 0) };
        var b = new WorkTicket { Number = 2, Date = new(2026, 8, 31), Time = new(9, 0) };
        var c = new WorkTicket { Number = 3, Date = new(2026, 8, 1), Time = new(23, 0) };

        Assert.Equal([2, 1, 3], new[] { a, b, c }.Newest().Select(t => t.Number));
    }

    [Fact]
    public void ApplyWork_ClosesAndReopens()
    {
        var t = new WorkTicket { Status = TicketStatus.InProgress };

        TicketJournal.ApplyWork(t, " Заменён отвод ", new DateOnly(2026, 8, 31));
        Assert.Equal(TicketStatus.Done, t.Status);
        Assert.Equal(new DateOnly(2026, 8, 31), t.DoneDate);
        Assert.Equal("Заменён отвод", t.WorkDone);

        TicketJournal.ApplyWork(t, "Заменён отвод и розетка", new DateOnly(2026, 9, 2));
        Assert.Equal(new DateOnly(2026, 8, 31), t.DoneDate);

        TicketJournal.ApplyWork(t, "", new DateOnly(2026, 9, 3));
        Assert.Equal(TicketStatus.InProgress, t.Status);
        Assert.Null(t.DoneDate);
    }
}
