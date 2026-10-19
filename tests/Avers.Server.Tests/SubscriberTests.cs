using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

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
    public async Task Card_PaidThroughCurrentMonth_HasNoDebt()
    {
        // Белоусов оплатил январь–август, рабочая дата 31.08: сентябрь–декабрь ещё не наступили.
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        var found = await client.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Account&q=1932041");
        var card = await client.GetFromJsonAsync<SubscriberCard>($"/api/subscribers/{Assert.Single(found!).Id}");

        Assert.Equal(0m, card!.Debt);
        Assert.Equal(0, card.UnpaidMonths);
        Assert.Equal(0m, Assert.Single(found!).Debt);
    }

    [Fact]
    public async Task Search_ShortSurname_MatchesWholeSurnameOnly()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        await app.WithDbAsync(async db =>
        {
            var house = await db.Houses.Select(h => h.Id).FirstAsync();
            db.Subscribers.Add(new Subscriber { HouseId = house, Account = "9000001", InnerCode = "9000001", LastName = "Ли", Apartment = "1" });
            db.Subscribers.Add(new Subscriber { HouseId = house, Account = "9000002", InnerCode = "9000002", LastName = "Лиханов", Apartment = "2" });
            return await db.SaveChangesAsync();
        });

        var li = await client.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Surname&q=ли");
        var prefix = await client.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Surname&q=Лиха");
        var empty = await client.GetAsync("/api/subscribers?mode=Surname&q=%20");

        Assert.Equal("Ли", Assert.Single(li!).FullName);
        Assert.Equal("Лиханов", Assert.Single(prefix!).FullName);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Card_UnknownSubscriber_Returns404()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/subscribers/999999")).StatusCode);
    }
}
