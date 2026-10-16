using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avers.Contracts;
using Avers.Data;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class DesktopTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData("sokolova", true, true)]
    [InlineData("lawyer", true, false)]
    [InlineData("novikov", false, false)]
    public async Task Snapshot_GivesOnlyWhatTheRoleMaySee(string login, bool subscribers, bool tickets)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync(login);

        var snapshot = (await client.GetFromJsonAsync<AversSnapshot>("/api/desktop/snapshot", Json))!.Link();

        Assert.NotEmpty(snapshot.Houses);
        Assert.All(snapshot.Houses, h => Assert.NotNull(h.Street));
        Assert.Equal(subscribers, snapshot.Subscribers.Count > 0);
        Assert.Equal(subscribers, snapshot.Payments.Count > 0);
        Assert.Equal(tickets, snapshot.Tickets.Count > 0);
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
