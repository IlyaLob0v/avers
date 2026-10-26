using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;

namespace Avers.Server.Tests;

public class ClientVersionTests
{
    private static async Task<HttpClient> LoginWithoutVersionAsync(AversApp app, string login)
    {
        var client = await app.LoginAsync(login);
        client.DefaultRequestHeaders.Remove(DesktopClient.Header);
        return client;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task OldDesktop_IsAskedToUpdate(string? version)
    {
        await using var app = new AversApp();
        var client = await LoginWithoutVersionAsync(app, "engineer");
        if (version is not null)
            client.DefaultRequestHeaders.Add(DesktopClient.Header, version);

        var response = await client.PutAsJsonAsync("/api/houses/1/passport", new HousePassportRequest("", "", "", "", "", ""));

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
        Assert.Contains("Обновите программу", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PhonePage_WorksWithoutVersion()
    {
        await using var app = new AversApp();
        var client = await LoginWithoutVersionAsync(app, "novikov");

        var response = await client.GetAsync("/api/mobile/houses?street=вол&house=19");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CurrentDesktop_Works()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/desktop/snapshot")).StatusCode);
    }
}
