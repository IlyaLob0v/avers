using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class SubscriberTests
{
    [PostgresFact]
    public async Task PhoneSearch_UsesNormalizedPhoneIndex()
    {
        await using var app = new AversApp();
        await app.WithDbAsync(async db =>
        {
            var house = await db.Houses.Select(h => h.Id).FirstAsync();
            for (var i = 1; i <= 10000; i++)
                db.Subscribers.Add(new Subscriber { HouseId = house, Account = $"PHONE-{i}", LastName = "Тест", Phone = $"8.9{(i * 104729L) % 1_000_000_000:D9}" });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("VACUUM ANALYZE \"Subscribers\"");
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """EXPLAIN (ANALYZE, FORMAT JSON) SELECT "Id" FROM "Subscribers" WHERE avers_phone_digits("Phone") LIKE '%9104729000%'""";
            var plan = (string)(await command.ExecuteScalarAsync())!;
            Assert.True(plan.Contains("IX_Subscribers_PhoneDigits", StringComparison.Ordinal), plan);
            return 0;
        });
    }

    [Fact]
    public async Task PhoneSearch_FindsDottedNumberWithExtension()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");
        await app.WithDbAsync(async db =>
        {
            var s = await db.Subscribers.FirstAsync();
            s.Phone = "8.900.123.45.67 доб.89";
            await db.SaveChangesAsync();
            return 0;
        });
        var rows = await client.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Phone&q=79001234567");
        Assert.Single(rows!);
    }

    [Fact]
    public async Task JackHistory_IsPagedAndPreservesOriginalAmounts()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("lawyer");
        await app.WithDbAsync(async db =>
        {
            for (var i = 0; i < 105; i++)
                db.JackPayments.Add(new JackPayment
                {
                    Id = 3_000_000_000L + i,
                    SubscriberId = 1,
                    Period = new DateTime(1996, 1, 1),
                    PaidAt = new DateTime(1996, 2, 1),
                    Amount = 15000,
                    Source = "Касса"
                });
            await db.SaveChangesAsync();
            return 0;
        });
        var response = await client.GetAsync("/api/subscribers/1/jack-payments?year=1996");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>());
        Assert.Equal(105, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(100, first.GetProperty("rows").GetArrayLength());
        Assert.Equal(15000, first.GetProperty("rows")[0].GetProperty("amount").GetDecimal());
        var next = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/subscribers/1/jack-payments?year=1996&offset=100");
        Assert.Equal(5, next.GetProperty("rows").GetArrayLength());
    }

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
    public async Task Search_ByContractNumber()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var found = await client.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Contract&q=2011/1800");

        Assert.Equal("180090", Assert.Single(found!).Account);
    }

    [Theory]
    [InlineData("8-983-623-3122")]
    [InlineData("+7 (983) 623-31-22")]
    [InlineData("6233122")]
    public async Task Search_ByPhone_IgnoresFormatAndCountryCode(string phone)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");

        var found = await client.GetFromJsonAsync<List<SubscriberListItem>>($"/api/subscribers?mode=Phone&q={Uri.EscapeDataString(phone)}");

        Assert.Equal("1932068", Assert.Single(found!).Account);
    }

    [Fact]
    public async Task Search_ByPhone_TooFewDigits_Returns400()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/subscribers?mode=Phone&q=31-22")).StatusCode);
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
