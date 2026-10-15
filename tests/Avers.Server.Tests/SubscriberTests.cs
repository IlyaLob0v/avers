using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;

namespace Avers.Server.Tests;

public class SubscriberTests
{
    [Fact]
    public async Task Streets_AndHouses_AreListed()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var streets = await client.GetFromJsonAsync<List<StreetDto>>("/api/streets");
        var avangard = Assert.Single(streets!, s => s.Name == "Авангардная");
        var houses = await client.GetFromJsonAsync<List<HouseDto>>($"/api/streets/{avangard.Id}/houses");

        Assert.Contains(houses!, h => h.Number == "12" && h.Address == "ул. Авангардная, д. 12");
    }

    [Fact]
    public async Task Search_ByAddress_IgnoresCaseOfCyrillic()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var found = await client.GetFromJsonAsync<List<SubscriberListItem>>(
            "/api/subscribers?mode=Address&street=авангард&house=12");

        Assert.NotEmpty(found!);
        Assert.All(found!, s => Assert.Equal("ул. Авангардная, д. 12", s.Address));
    }

    [Fact]
    public async Task Search_ByAccount_FindsCardWithSameDebt()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        var card = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/1");

        var found = await client.GetFromJsonAsync<List<SubscriberListItem>>($"/api/subscribers?mode=Account&q={card!.Account}");

        var item = Assert.Single(found!, s => s.Id == 1);
        Assert.Equal(card.Debt, item.Debt);
        Assert.Equal(card.Charged - card.Paid, card.Debt);
    }

    [Fact]
    public async Task Search_BySurname_NeedsFourLetters()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var response = await client.GetAsync("/api/subscribers?mode=Surname&q=Ива");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Card_UnknownSubscriber_Returns404()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/subscribers/999999")).StatusCode);
    }
}
