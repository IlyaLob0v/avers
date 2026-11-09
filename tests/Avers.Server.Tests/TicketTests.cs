using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avers.Server.Tests;

public class TicketTests
{
    private static async Task<HouseDto> Vol19dAsync(HttpClient client)
    {
        var streets = await client.GetFromJsonAsync<List<StreetDto>>("/api/streets");
        var street = Assert.Single(streets!, s => s.Name == "Волочаевская");
        var houses = await client.GetFromJsonAsync<List<HouseDto>>($"/api/streets/{street.Id}/houses");
        return Assert.Single(houses!, h => h.Number == "19Д");
    }

    [Fact]
    public async Task List_ByHousePeriodAndKind()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var house = await Vol19dAsync(client);

        var list = await client.GetFromJsonAsync<List<TicketDto>>(
            $"/api/tickets?houseId={house.Id}&from=2026-08-01&to=2026-08-31&kind=Antenna");

        Assert.NotEmpty(list!);
        Assert.All(list!, t =>
        {
            Assert.Equal(house.Id, t.HouseId);
            Assert.Equal("Antenna", t.Kind);
            Assert.InRange(t.Date, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        });
        Assert.Equal(list!.OrderByDescending(t => t.Date).ThenByDescending(t => t.Time).Select(t => t.Number), list!.Select(t => t.Number));
        Assert.All(list!, t => Assert.Equal("ООО «Жилсервис-Центр»", t.Homeowner));
    }

    [Fact]
    public async Task List_ByYear_ExcludesOtherYears()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var y2025 = await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets?year=2025");
        var all = await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets");

        Assert.NotEmpty(y2025!);
        Assert.All(y2025!, t => Assert.Equal(2025, t.Date.Year));
        Assert.True(all!.Count > y2025!.Count);
    }

    [Fact]
    public async Task List_OnlyOverdue_MatchesOpenTicketsPastDue()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var day = new DateOnly(2026, 11, 9);

        var open = await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets?open=true");
        var overdue = await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets?overdueOn=2026-11-09");

        var expected = open!.Where(t => t.Status is "Accepted" or "InProgress" && t.Date.AddDays(TicketJournal.DueDays) < day)
            .Select(t => t.Id).Order().ToList();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, overdue!.Select(t => t.Id).Order().ToList());
    }

    [Fact]
    public async Task ByMonth_GivesTwelveMonths()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var months = await client.GetFromJsonAsync<List<TicketMonthDto>>("/api/tickets/by-month?year=2026");
        var all2026 = await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets?year=2026");

        Assert.Equal(12, months!.Count);
        Assert.Equal(all2026!.Count, months.Sum(m => m.Accepted));
        Assert.All(months, m => Assert.Equal(m.Accepted, m.Done + m.Open));
    }

    [Fact]
    public async Task ByMonth_ForApartment_MatchesJournalOfApartment()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");
        var house = await Vol19dAsync(client);

        var months = (await client.GetFromJsonAsync<List<TicketMonthDto>>($"/api/tickets/by-month?year=2026&houseId={house.Id}&apartment=68"))!;
        var journal = await client.GetFromJsonAsync<List<TicketDto>>($"/api/tickets?year=2026&houseId={house.Id}&apartment=68");

        Assert.NotEmpty(journal!);
        Assert.Equal(journal!.Count, months.Sum(m => m.Accepted));
        Assert.Equal(journal.Count(t => t.Status == "Done"), months.Sum(m => m.Done));
        Assert.Equal(journal.GroupBy(t => t.Date.Month).ToDictionary(g => g.Key, g => g.Count()),
            months.Where(m => m.Accepted > 0).ToDictionary(m => m.Month, m => m.Accepted));
    }

    [Fact]
    public async Task Years_FromFirstTicketToCurrent()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");

        Assert.Equal([2026, 2025], await client.GetFromJsonAsync<List<int>>("/api/tickets/years"));
    }

    [Fact]
    public async Task ByHouse_CountsTicketsOfPeriod()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var stats = (await client.GetFromJsonAsync<List<TicketHouseCountDto>>("/api/tickets/by-house?from=2026-08-01&to=2026-08-31"))!;
        var journal = await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets?from=2026-08-01&to=2026-08-31");

        Assert.Equal(journal!.Count, stats.Sum(s => s.Accepted));
        Assert.Equal(journal.Count(t => t.Status == "Done"), stats.Sum(s => s.Done));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tickets/by-house?from=2026-09-01&to=2026-08-01")).StatusCode);
    }

    [Fact]
    public async Task List_RowsCarryAuthorAndShortNameOnly()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");
        var sokolovaId = await app.WithDbAsync(db => db.Users.Where(u => u.Login == "sokolova").Select(u => u.Id).FirstAsync());

        var row = Assert.Single((await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets?q=1001"))!);

        Assert.Equal(sokolovaId, row.AcceptedByUserId);
        Assert.Equal(("Иванова", "Мария", "Петровна", "1932068"), (row.SubscriberLastName, row.SubscriberFirstName, row.SubscriberPatronymic, row.Account));
    }

    [Fact]
    public async Task Apartment_OfOpenTicket_ContractPaymentNotes_LoggedAsView()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlova");
        var all = (await client.GetFromJsonAsync<List<TicketDto>>("/api/tickets"))!;
        var foreign = Assert.Single(all, t => t.Number == 1001);
        var withoutSubscriber = Assert.Single(all, t => t.Number == 812);
        var expected = await app.WithDbAsync(db => db.Subscribers.FirstAsync(s => s.Id == foreign.SubscriberId));

        var apartment = await client.GetFromJsonAsync<TicketApartmentDto>($"/api/tickets/{foreign.Id}/apartment");

        Assert.False(foreign.CanEdit);
        Assert.Equal((expected.Id, expected.HouseId, expected.LastName, expected.Account, expected.ContractNumber, expected.ContractDate, expected.LastPaymentDate, expected.Notes),
            (apartment!.SubscriberId, apartment.HouseId, apartment.LastName, apartment.Account, apartment.ContractNumber, apartment.ContractDate, apartment.LastPaymentDate, apartment.Notes));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tickets/{withoutSubscriber.Id}/apartment")).StatusCode);
        var log = (await (await app.LoginAsync("admin")).GetFromJsonAsync<List<AccessLogEntry>>("/api/access-log"))!;
        Assert.Contains(log, e => e.Action == "Квартира заявки" && e.Target.Contains("1932068"));
    }

    [Theory]
    [InlineData("/api/tickets/years")]
    [InlineData("/api/tickets/by-house?from=2026-08-01&to=2026-08-31")]
    [InlineData("/api/tickets/by-month?year=2026")]
    [InlineData("/api/tickets/1/apartment")]
    public async Task TicketSummaries_ClosedToLawyer(string url)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("lawyer");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Subscriber_WithoutApartment_GetsOnlyOwnTickets()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var found = await client.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Account&q=1932068");
        var ivanova = Assert.Single(found!);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AversDbContext>();
            db.Subscribers.Single(s => s.Id == ivanova.Id).Apartment = "";
            db.SaveChanges();
        }
        var created = await client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest(ivanova.HouseId, "Нет сигнала в подъезде", "Antenna", Section: "1"));
        created.EnsureSuccessStatusCode();
        var houseTicket = await created.Content.ReadFromJsonAsync<TicketDto>();

        var list = await client.GetFromJsonAsync<List<TicketDto>>($"/api/subscribers/{ivanova.Id}/tickets");

        Assert.DoesNotContain(list!, t => t.Id == houseTicket!.Id);
        Assert.All(list!, t => Assert.Equal(ivanova.Id, t.SubscriberId));
    }

    [Fact]
    public async Task Subscriber_TicketsOfApartment()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var found = await client.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Account&q=1932068");
        var ivanova = Assert.Single(found!);

        var list = await client.GetFromJsonAsync<List<TicketDto>>($"/api/subscribers/{ivanova.Id}/tickets");

        Assert.True(list!.Count >= 3);
        Assert.All(list, t => Assert.Equal("68", t.Apartment));
    }

    [Fact]
    public async Task ServiceInfo_ShowsHomeownerAndOpenFault()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");
        var house = await Vol19dAsync(client);

        var info = await client.GetFromJsonAsync<HouseServiceInfoDto>($"/api/houses/{house.Id}/service-info");

        Assert.Equal("ООО «Жилсервис-Центр»", info!.Homeowner);
        Assert.NotNull(info.OpenFault);
        Assert.True(info.OpenTickets > 0);
    }

    [Fact]
    public async Task Dispatcher_CreatesAndEditsOwnTicket()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");
        var house = await Vol19dAsync(client);

        var response = await client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest(house.Id, "Нет изображения", Apartment: "12", Section: "1"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Equal("Соколова Н.В.", created.AcceptedBy);
        Assert.Equal(new DateOnly(2026, 8, 31), created.Date);
        Assert.True(created.CanEdit);

        var update = await client.PutAsJsonAsync($"/api/tickets/{created.Id}",
            new UpdateTicketRequest(WorkDone: "Заменён штекер", Mechanic: "Малышев Р.А."));
        update.EnsureSuccessStatusCode();
        var done = (await update.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Equal("Done", done.Status);
        Assert.Equal(new DateOnly(2026, 8, 31), done.DoneDate);
    }

    [Fact]
    public async Task Dispatcher_SetsDoneDateAndDefersWithWorkNote()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");
        var house = await Vol19dAsync(client);
        var created = (await (await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(house.Id, "Нет сигнала")))
            .Content.ReadFromJsonAsync<TicketDto>())!;

        var deferred = await client.PutAsJsonAsync($"/api/tickets/{created.Id}",
            new UpdateTicketRequest(WorkDone: "Осмотр, нет доступа на техэтаж", Status: "Deferred"));
        var future = await client.PutAsJsonAsync($"/api/tickets/{created.Id}",
            new UpdateTicketRequest(WorkDone: "Заменён штекер", Status: "Done", DoneDate: new DateOnly(2026, 9, 1)));
        var beforeAccepted = await client.PutAsJsonAsync($"/api/tickets/{created.Id}",
            new UpdateTicketRequest(WorkDone: "Заменён штекер", Status: "Done", DoneDate: new DateOnly(2026, 8, 30)));
        var done = await client.PutAsJsonAsync($"/api/tickets/{created.Id}",
            new UpdateTicketRequest(WorkDone: "Заменён штекер", Status: "Done", DoneDate: new DateOnly(2026, 8, 31)));

        var d = (await deferred.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Equal(("Deferred", (DateOnly?)null), (d.Status, d.DoneDate));
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (future.StatusCode, beforeAccepted.StatusCode));
        var t = (await done.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Equal(("Done", (DateOnly?)new DateOnly(2026, 8, 31)), (t.Status, t.DoneDate));
    }

    [Fact]
    public async Task Create_CallerPhone_OverridesCardPhone()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");
        var house = await Vol19dAsync(client);

        var response = await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(house.Id, "Нет сигнала", Phone: "8-913-000-00-00"));

        Assert.Equal("8-913-000-00-00", (await response.Content.ReadFromJsonAsync<TicketDto>())!.Phone);
    }

    [Fact]
    public async Task Dispatcher_CannotEditSomeoneElsesTicket_HeadCan()
    {
        await using var app = new AversApp();
        var dispatcher = await app.LoginAsync("sokolova");
        var all = await dispatcher.GetFromJsonAsync<List<TicketDto>>("/api/tickets");
        var foreign = Assert.Single(all!, t => t.Number == 994);
        Assert.Equal("Орлова Т.И.", foreign.AcceptedBy);
        Assert.False(foreign.CanEdit);
        Assert.Contains(all!, t => t.CanEdit);

        var denied = await dispatcher.PutAsJsonAsync($"/api/tickets/{foreign.Id}", new UpdateTicketRequest(Mechanic: "Иванов"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var head = await app.LoginAsync("orlov");
        var allowed = await head.PutAsJsonAsync($"/api/tickets/{foreign.Id}", new UpdateTicketRequest(Mechanic: "Иванов"));
        allowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChiefEngineer_SeesTickets_CannotCreate()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");
        var house = await Vol19dAsync(client);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tickets")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(house.Id, "Нет сигнала"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lawyer_HasNoTickets()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("lawyer");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task Create_Validates()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var house = await Vol19dAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(house.Id, " "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(999999, "Нет сигнала"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(house.Id, "Нет", Kind: "Космос"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tickets?month=5")).StatusCode);
    }
}
