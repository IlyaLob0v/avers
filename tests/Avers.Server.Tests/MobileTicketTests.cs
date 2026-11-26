using System.Net;
using System.Text.Json;
using Avers.Domain;

namespace Avers.Server.Tests;

public class MobileTicketTests
{
    [Fact]
    public async Task Reader_SeesOnlyOpenTicketsOfChosenHouse_WithoutSubscriberDetails()
    {
        await using var app = new AversApp();
        var houseId = await app.WithDbAsync(db =>
        {
            var houses = db.Houses.OrderBy(h => h.Id).Take(2).ToList();
            db.Tickets.RemoveRange(db.Tickets);
            db.Tickets.AddRange(
                Ticket(houses[0].Id, 901, TicketStatus.Accepted),
                Ticket(houses[0].Id, 902, TicketStatus.Done),
                Ticket(houses[1].Id, 903, TicketStatus.Accepted));
            db.SaveChanges();
            return Task.FromResult(houses[0].Id);
        });
        var client = await app.LoginAsync("novikov");

        var response = await client.GetAsync($"/api/mobile/houses/{houseId}/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        var ticket = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(901, ticket.GetProperty("number").GetInt32());
        Assert.Equal("68", ticket.GetProperty("apartment").GetString());
        Assert.Equal("2", ticket.GetProperty("section").GetString());
        Assert.Equal("Нет сигнала", ticket.GetProperty("fault").GetString());
        Assert.Equal("Механик", ticket.GetProperty("mechanic").GetString());
        var names = ticket.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("phone", names);
        Assert.DoesNotContain("subscriber", names);
        Assert.DoesNotContain("subscriberId", names);
        Assert.DoesNotContain("account", names);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("lawyer", HttpStatusCode.Forbidden)]
    public async Task TicketOverview_RequiresHouseReadAccess(string? login, HttpStatusCode expected)
    {
        await using var app = new AversApp();
        var client = login is null ? app.CreateClient() : await app.LoginAsync(login);
        Assert.Equal(expected, (await client.GetAsync("/api/mobile/houses/1/tickets")).StatusCode);
    }

    [Fact]
    public async Task MissingHouse_DoesNotLookLikeAnEmptyTicketList()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("novikov");
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/mobile/houses/2147483647/tickets")).StatusCode);
    }

    [Fact]
    public async Task LargeHouse_ReportsTotalInsteadOfSilentlyHidingTheLimit()
    {
        await using var app = new AversApp();
        var houseId = await app.WithDbAsync(db =>
        {
            var id = db.Houses.OrderBy(h => h.Id).First().Id;
            db.Tickets.RemoveRange(db.Tickets);
            db.Tickets.AddRange(Enumerable.Range(1, 201).Select(number => Ticket(id, number, TicketStatus.Accepted)));
            db.SaveChanges();
            return Task.FromResult(id);
        });
        var client = await app.LoginAsync("novikov");
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/mobile/houses/{houseId}/tickets"));
        Assert.Equal(201, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(200, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(201, json.RootElement.GetProperty("items")[0].GetProperty("number").GetInt32());
    }

    private static WorkTicket Ticket(int houseId, int number, TicketStatus status) => new()
    {
        HouseId = houseId,
        Number = number,
        Date = new(2026, 8, 31),
        Status = status,
        Section = "2",
        Apartment = "68",
        Fault = "Нет сигнала",
        Mechanic = "Механик",
        Phone = "Не выдавать читателю"
    };
}
