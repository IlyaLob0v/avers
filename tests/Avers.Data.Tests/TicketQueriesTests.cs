using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data.Tests;

/// <summary>Чтение заявок частями: журнал, сводки и квартира заявки — одни запросы для сервера и учебной базы.</summary>
public class TicketQueriesTests
{
    private static SqliteDatabase Seeded()
    {
        var database = new SqliteDatabase();
        using var db = database.CreateContext();
        DemoSeeder.EnsureSeeded(db);
        return database;
    }

    [Fact]
    public void ByMonth_CountedByDatabase_SameAsJournalInMemory()
    {
        using var database = Seeded();
        using var db = database.CreateContext();
        var all = db.Tickets.AsNoTracking().ToList();
        var houseId = all.GroupBy(t => t.HouseId).OrderByDescending(g => g.Count()).First().Key;

        Assert.Equal(TicketJournal.ByMonth(all, 2026), TicketQueries.ByMonth(db, 2026, new TicketFilter()));
        Assert.Equal(TicketJournal.ByMonth(all.Where(t => t.HouseId == houseId), 2026),
            TicketQueries.ByMonth(db, 2026, new TicketFilter { HouseId = houseId }));
    }

    [Fact]
    public void Page_RowsCarryOnlyNameAndAccountOfSubscriber()
    {
        using var database = Seeded();
        using var db = database.CreateContext();

        var page = TicketQueries.Page(db, new TicketFilter());

        var row = page.First(t => t.Number == DemoSeeder.StartTicket);
        Assert.Equal(("Иванова", "Мария", "Петровна", DemoSeeder.StartAccount), (row.Subscriber!.LastName, row.Subscriber.FirstName, row.Subscriber.Patronymic, row.Subscriber.Account));
        Assert.Equal(("", "", (DateOnly?)null, (DateOnly?)null), (row.Subscriber.Notes, row.Subscriber.ContractNumber, row.Subscriber.ContractDate, row.Subscriber.LastPaymentDate));
        Assert.Equal("Волочаевская", row.House.Street.Name);
        Assert.Equal(page.Newest().Select(t => t.Id), page.Select(t => t.Id));
    }

    [Fact]
    public void Page_StopsAtMaxResults_NewestFirst()
    {
        using var database = Seeded();
        using (var db = database.CreateContext())
        {
            var houseId = db.Houses.Select(h => h.Id).First();
            for (var i = 0; i < TicketQueries.MaxResults; i++)
                db.Tickets.Add(new WorkTicket { Number = 10_000 + i, Date = new DateOnly(2020, 1, 1).AddDays(i % 300), HouseId = houseId, Fault = "Старая заявка" });
            db.SaveChanges();
        }
        using var check = database.CreateContext();

        var page = TicketQueries.Page(check, new TicketFilter());

        Assert.Equal(TicketQueries.MaxResults, page.Count);
        Assert.Contains(page, t => t.Number == DemoSeeder.StartTicket);
    }

    [Fact]
    public void Years_FromFirstTicketToCurrentYear()
    {
        using var database = Seeded();
        using var db = database.CreateContext();

        Assert.Equal([2027, 2026, 2025], TicketQueries.Years(db, new DateOnly(2027, 2, 1)));
    }

    [Fact]
    public void ByHouse_CountsAcceptedAndDoneOfPeriod()
    {
        using var database = Seeded();
        using var db = database.CreateContext();
        var august = db.Tickets.AsNoTracking().Where(t => t.Date >= new DateOnly(2026, 8, 1) && t.Date <= new DateOnly(2026, 8, 31)).ToList();

        var stats = TicketQueries.ByHouse(db, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        Assert.Equal(august.Count, stats.Sum(s => s.Accepted));
        Assert.Equal(august.Count(t => t.Status == TicketStatus.Done), stats.Sum(s => s.Done));
        Assert.Equal(august.Select(t => t.HouseId).Distinct().Order(), stats.Select(s => s.HouseId).Order());
    }

    [Fact]
    public void Apartment_OfTicket_ContractPaymentAndNotes()
    {
        using var database = Seeded();
        using var db = database.CreateContext();
        var ticket = db.Tickets.Single(t => t.Number == DemoSeeder.StartTicket);
        var withoutSubscriber = db.Tickets.Single(t => t.Number == 812);
        var full = db.Subscribers.AsNoTracking().Single(s => s.Id == ticket.SubscriberId);

        var s = TicketQueries.Apartment(db, ticket.Id)!;

        Assert.Equal((full.Id, full.HouseId, full.FullName, full.Account), (s.Id, s.HouseId, s.FullName, s.Account));
        Assert.Equal((full.ContractNumber, full.ContractDate, full.ContractHolder, full.LastPaymentDate, full.Notes, full.PzhreuIndex),
            (s.ContractNumber, s.ContractDate, s.ContractHolder, s.LastPaymentDate, s.Notes, s.PzhreuIndex));
        Assert.Null(TicketQueries.Apartment(db, withoutSubscriber.Id));
    }

    [Fact]
    public void ForApartment_OwnTicketsAndTicketsByAddress()
    {
        using var database = Seeded();
        using var db = database.CreateContext();
        var ivanova = db.Subscribers.AsNoTracking().Single(s => s.Account == DemoSeeder.StartAccount);
        var expected = ApartmentTickets.For(db.Tickets.AsNoTracking().ToList(), ivanova).Select(t => t.Number).ToList();

        var tickets = TicketQueries.ForApartment(db, ivanova.Id)!;

        Assert.Contains(812, expected);
        Assert.Equal(expected, tickets.Select(t => t.Number));
        Assert.Null(TicketQueries.ForApartment(db, -1));
    }
}
