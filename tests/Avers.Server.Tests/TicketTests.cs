using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Data;
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
        Assert.All(list, t => Assert.Equal("ООО «Жилсервис-Центр»", t.Homeowner));
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
    public async Task Subscriber_WithoutApartment_GetsOnlyOwnTickets()
    {
        // Абонент без номера квартиры (частный дом, недозаполненная карточка) не должен получать
        // все заявки дома, принятые без квартиры.
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
