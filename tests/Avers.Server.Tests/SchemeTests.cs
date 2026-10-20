using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Domain;

namespace Avers.Server.Tests;

public class SchemeTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];

    private static async Task<MobileHouseDto> HouseAsync(HttpClient client, string street, string number)
    {
        var id = (await client.GetFromJsonAsync<List<HouseDto>>($"/api/mobile/houses?street={street}&house={number}"))!
            .First(h => h.Address.EndsWith($"д. {number}")).Id;
        return (await client.GetFromJsonAsync<MobileHouseDto>($"/api/mobile/houses/{id}"))!;
    }

    [Fact]
    public async Task EngineerUploads_MechanicSeesImage()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var mechanic = await app.LoginAsync("novikov");
        var house = await HouseAsync(mechanic, "Ленина", "44");

        var upload = await engineer.PostAsJsonAsync($"/api/houses/{house.Id}/schemes", new AddSchemeRequest("Стояки 1-4 подъезд", Png));
        upload.EnsureSuccessStatusCode();

        var scheme = (await HouseAsync(mechanic, "Ленина", "44")).Schemes.Single(s => s.Name == "Стояки 1-4 подъезд");
        Assert.True(scheme.HasImage);
        Assert.Equal("PNG", scheme.Format);
        Assert.Equal(AversApp.Today, scheme.UpdatedAt);
        var image = await mechanic.GetAsync($"/api/mobile/schemes/{scheme.Id}");
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ReplaceFile_ChangesImageAndIsLogged()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var scheme = (await HouseAsync(engineer, "Ленина", "44")).Schemes.Single();

        var response = await engineer.PutAsJsonAsync($"/api/schemes/{scheme.Id}/file", new SchemeFileRequest(Jpeg));
        response.EnsureSuccessStatusCode();

        var image = await engineer.GetAsync($"/api/schemes/{scheme.Id}/file");
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Jpeg, await image.Content.ReadAsByteArrayAsync());
        var log = await (await app.LoginAsync("admin")).GetFromJsonAsync<List<ChangeLogEntry>>("/api/change-log");
        var entry = log!.First();
        Assert.Equal("Схема СКПТ", entry.Action);
        Assert.Equal("ул. Ленина, д. 44", entry.Target);
        Assert.Equal("Заменён файл: Схема СКПТ Ленина 44", entry.Changes);
    }

    [Fact]
    public async Task RenameAndDelete_AreLogged()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var house = await HouseAsync(engineer, "Ленина", "44");
        (await engineer.PostAsJsonAsync($"/api/houses/{house.Id}/schemes", new AddSchemeRequest("IMG_2031", Png))).EnsureSuccessStatusCode();
        var id = (await HouseAsync(engineer, "Ленина", "44")).Schemes.Single(s => s.Name == "IMG_2031").Id;

        (await engineer.PutAsJsonAsync($"/api/schemes/{id}/name", new RenameSchemeRequest("Стояки, подъезд 1"))).EnsureSuccessStatusCode();
        Assert.Contains((await HouseAsync(engineer, "Ленина", "44")).Schemes, s => s.Name == "Стояки, подъезд 1");

        (await engineer.DeleteAsync($"/api/schemes/{id}")).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await HouseAsync(engineer, "Ленина", "44")).Schemes, s => s.Id == id);
        Assert.Equal(HttpStatusCode.NotFound, (await engineer.GetAsync($"/api/schemes/{id}/file")).StatusCode);

        var log = await (await app.LoginAsync("admin")).GetFromJsonAsync<List<ChangeLogEntry>>("/api/change-log");
        Assert.Equal(["Удалена: Стояки, подъезд 1", "Название: IMG_2031 → Стояки, подъезд 1"], log!.Take(2).Select(e => e.Changes));
    }

    [Fact]
    public async Task EmptyName_IsRejected()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var scheme = (await HouseAsync(engineer, "Ленина", "44")).Schemes.Single();

        var response = await engineer.PutAsJsonAsync($"/api/schemes/{scheme.Id}/name", new RenameSchemeRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NotAnImage_IsRejected()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var house = await HouseAsync(engineer, "Ленина", "44");

        var response = await engineer.PostAsJsonAsync($"/api/houses/{house.Id}/schemes", new AddSchemeRequest("Ведомость", "PK\u0003\u0004"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dispatcher_CannotChangeSchemes()
    {
        await using var app = new AversApp();
        var dispatcher = await app.LoginAsync("orlova");
        var house = await HouseAsync(await app.LoginAsync("novikov"), "Ленина", "44");

        var upload = await dispatcher.PostAsJsonAsync($"/api/houses/{house.Id}/schemes", new AddSchemeRequest("Схема", Png));
        var delete = await dispatcher.DeleteAsync($"/api/schemes/{house.Schemes.Single().Id}");

        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }
}
