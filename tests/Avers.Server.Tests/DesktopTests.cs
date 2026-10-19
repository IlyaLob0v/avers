using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class DesktopTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData("sokolova", true)]
    [InlineData("lawyer", false)]
    [InlineData("novikov", false)]
    public async Task Snapshot_GivesOnlyWhatTheRoleMaySee(string login, bool tickets)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync(login);

        var snapshot = (await client.GetFromJsonAsync<AversSnapshot>("/api/desktop/snapshot", Json))!.Link();

        Assert.NotEmpty(snapshot.Houses);
        Assert.All(snapshot.Houses, h => Assert.NotNull(h.Street));
        Assert.Equal(tickets, snapshot.Tickets.Count > 0);
        Assert.Equal(tickets, snapshot.Subscribers.Count > 0);
    }

    [Fact]
    public async Task Snapshot_HasOnlySubscribersOfTickets()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");

        var snapshot = (await client.GetFromJsonAsync<AversSnapshot>("/api/desktop/snapshot", Json))!.Link();

        var inTickets = snapshot.Tickets.Where(t => t.SubscriberId is not null).Select(t => t.SubscriberId!.Value).ToHashSet();
        Assert.Equal(inTickets.Order(), snapshot.Subscribers.Select(s => s.Id).Order());
        Assert.All(snapshot.Tickets.Where(t => t.SubscriberId is not null), t => Assert.NotNull(t.Subscriber));
    }

    [Fact]
    public async Task HouseSubscribers_ComeWithPaymentsOfThatHouseOnly()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var houseId = await app.WithDbAsync(db => db.Subscribers.Select(s => s.HouseId).FirstAsync());

        var house = await client.GetFromJsonAsync<HouseSubscribers>($"/api/houses/{houseId}/subscribers", Json);

        Assert.NotEmpty(house!.Subscribers);
        Assert.All(house.Subscribers, s => Assert.Equal(houseId, s.HouseId));
        var ids = house.Subscribers.Select(s => s.Id).ToHashSet();
        Assert.NotEmpty(house.Payments);
        Assert.All(house.Payments, p => Assert.Contains(p.SubscriberId, ids));
        var expected = await app.WithDbAsync(db => db.Payments.CountAsync(p => p.Subscriber.HouseId == houseId));
        Assert.Equal(expected, house.Payments.Count);
    }

    [Theory]
    [InlineData("/api/houses/1/subscribers")]
    [InlineData("/api/debts")]
    [InlineData("/api/reports/accounting?year=2026&month=8")]
    public async Task SubscriberData_IsClosedToReader(string url)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("novikov");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Debts_MatchBillingOfEachSubscriber()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("lawyer");

        var debts = (await client.GetFromJsonAsync<Debts>("/api/debts", Json))!;

        var subscribers = await app.WithDbAsync(db => db.Subscribers.Include(s => s.Payments).ToListAsync());
        var debtOf = subscribers.ToDictionary(s => s.Id, s => Billing.Debt(Billing.Due(s.Payments, AversApp.Today)));
        var expected = subscribers.Where(s => debtOf[s.Id] >= Billing.DebtHighlight || Billing.IsOldPayer(s.LastPaymentDate, AversApp.Today));
        Assert.NotEmpty(debts.Debtors);
        Assert.Equal(expected.Select(s => s.Id), debts.Debtors.Select(d => d.SubscriberId));
        Assert.All(debts.Debtors, d => Assert.Equal(debtOf[d.SubscriberId], d.Debt));
        foreach (var house in subscribers.GroupBy(s => s.HouseId))
            Assert.Equal(house.Sum(s => debtOf[s.Id]), debts.ByHouse[house.Key]);
    }

    [Fact]
    public async Task Accounting_SameAsOverAllPayments()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var report = (await client.GetFromJsonAsync<AccountingReport>("/api/reports/accounting?year=2026&month=8", Json))!;

        var all = await app.WithDbAsync(db => db.Payments.ToListAsync());
        Assert.NotEmpty(report.Shipment);
        Assert.NotEmpty(report.Receipts);
        Assert.Equal(AccountingMonth.Shipment(all, 2026, 8), report.Shipment);
        Assert.Equal(AccountingMonth.Receipts(all, 2026, 8), report.Receipts);
    }

    [Fact]
    public async Task Snapshot_DoesNotLeakUsers()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var body = await client.GetStringAsync("/api/desktop/snapshot");

        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pbkdf2", body);
    }

    [Fact]
    public async Task Passport_SavedByChiefEngineerOnly()
    {
        await using var app = new AversApp();
        var request = new HousePassportRequest("Антенна ДМВ", "РК-75", "Terra HA-126", "БП 12 В", "у старшего по дому", "");

        var dispatcher = await (await app.LoginAsync("sokolova")).PutAsJsonAsync("/api/houses/1/passport", request);
        var engineer = await (await app.LoginAsync("engineer")).PutAsJsonAsync("/api/houses/1/passport", request);

        Assert.Equal(HttpStatusCode.Forbidden, dispatcher.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, engineer.StatusCode);
        var amplifiers = await app.WithDbAsync(db => db.Houses.Where(h => h.Id == 1).Select(h => h.Amplifiers).SingleAsync());
        Assert.Equal("Terra HA-126", amplifiers);
    }
}
