using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Avers.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class MapTests
{
    [Fact]
    public async Task Dispatcher_SeesHousesWithPointsAndFaults()
    {
        await using var app = new AversApp();
        await app.WithDbAsync(async db =>
        {
            var house = await db.Houses.Include(h => h.Street).SingleAsync(h => h.Street.Name == "Братская" && h.Number == "18");
            (house.Latitude, house.Longitude) = (55.02, 73.30);
            return await db.SaveChangesAsync();
        });
        var client = await app.LoginAsync("sokolova");

        var map = await client.GetFromJsonAsync<MapHousesDto>("/api/mobile/map/houses");
        var houses = map!.Houses;

        Assert.False(map.CanPlace);
        var placed = houses.Single(h => h.Latitude is not null);
        Assert.Equal(("ул. Братская, д. 18", 55.02, 73.30, true), (placed.Address, placed.Latitude, placed.Longitude, placed.HasOpenFault));
        Assert.Contains(houses!, h => h.Address == "ул. Волочаевская, д. 19" && h.Latitude is null);
    }

    [Fact]
    public async Task ChiefEngineer_PutsHouseOnMap_AndItIsLogged()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");
        var id = (await client.GetFromJsonAsync<MapHousesDto>("/api/mobile/map/houses"))!.Houses.First().Id;

        var response = await client.PutAsJsonAsync($"/api/mobile/map/houses/{id}", new MapPointDto(54.98, 73.37));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var map = await client.GetFromJsonAsync<MapHousesDto>("/api/mobile/map/houses");
        Assert.True(map!.CanPlace);
        var house = map.Houses.Single(h => h.Id == id);
        Assert.Equal((54.98, 73.37), (house.Latitude, house.Longitude));
        Assert.True(await app.WithDbAsync(db => db.ChangeLog.AnyAsync(e => e.Action == "Карта")));
    }

    [Fact]
    public async Task Mechanic_CannotMoveHouse()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("novikov");

        var response = await client.PutAsJsonAsync("/api/mobile/map/houses/1", new MapPointDto(54.98, 73.37));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PointOutsideTheWorld_IsRejected()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");

        var response = await client.PutAsJsonAsync("/api/mobile/map/houses/1", new MapPointDto(154, 73));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MapFile_IsServedInPartsToLoggedInStaff()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllBytesAsync(file, Enumerable.Range(0, 100).Select(i => (byte)i).ToArray());
        try
        {
            await using var app = new AversApp();
            using var withMap = app.WithWebHostBuilder(b => b.UseSetting("Avers:MapFile", file));
            var client = await LoginAsync(withMap, "novikov");
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/mobile/map/tiles");
            request.Headers.Range = new RangeHeaderValue(10, 12);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
            Assert.Equal([10, 11, 12], await response.Content.ReadAsByteArrayAsync());
            Assert.Equal(HttpStatusCode.Unauthorized, (await withMap.CreateClient().GetAsync("/api/mobile/map/tiles")).StatusCode);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task NoMapFile_Is404()
    {
        await using var app = new AversApp();
        using var withMap = app.WithWebHostBuilder(b => b.UseSetting("Avers:MapFile", "/нет/такого/файла.pmtiles"));
        var client = await LoginAsync(withMap, "novikov");

        var response = await client.GetAsync("/api/mobile/map/tiles");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MapImportCommand_PlacesDemoHouseFromOverpassFile()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, """
            {"elements":[{"type":"way","id":1,"center":{"lat":55.03,"lon":73.28},"tags":{"addr:street":"улица Братская","addr:housenumber":"18"}}]}
            """);
        try
        {
            await using var app = new AversApp();
            var output = new StringWriter();

            var code = await Avers.Server.Hosting.AdminCommands.MapImportAsync(app.Services, file, output);

            Assert.Equal(0, code);
            Assert.StartsWith("Поставлено на карту домов: 1.", output.ToString());
            Assert.Equal(55.03, await app.WithDbAsync(db => db.Houses.Where(h => h.Number == "18" && h.Street.Name == "Братская").Select(h => h.Latitude).SingleAsync()));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string login)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password = "avers" });
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    private sealed record TokenResponse(string AccessToken);
}
