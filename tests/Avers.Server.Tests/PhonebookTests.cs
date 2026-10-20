using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class PhonebookTests
{
    private static HouseRequest House(int streetId, string number) =>
        new(streetId, number, "0402", "1200999", 9, 4, 4, 144, "Центр", null, true);

    private static async Task<string> ErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Problem>();
        return problem!.Errors["phonebook"].Single();
    }

    [Fact]
    public async Task Engineer_AddsStreetAndHouse_ThenDeletesThem()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");

        (await engineer.PostAsJsonAsync("/api/streets", new StreetRequest(" Звёздная "))).EnsureSuccessStatusCode();
        var streetId = await app.WithDbAsync(db => db.Streets.Where(s => s.Name == "Звёздная").Select(s => s.Id).SingleAsync());
        (await engineer.PostAsJsonAsync("/api/houses", House(streetId, "7А"))).EnsureSuccessStatusCode();
        var house = await app.WithDbAsync(db => db.Houses.SingleAsync(h => h.StreetId == streetId));
        Assert.Equal(("7А", 144, true), (house.Number, house.Apartments, house.Serviced));

        Assert.Equal("На улице Звёздная есть дома (1). Сначала удалите их.", await ErrorAsync(await engineer.DeleteAsync($"/api/streets/{streetId}")));
        (await engineer.DeleteAsync($"/api/houses/{house.Id}")).EnsureSuccessStatusCode();
        (await engineer.DeleteAsync($"/api/streets/{streetId}")).EnsureSuccessStatusCode();

        var log = await (await app.LoginAsync("admin")).GetFromJsonAsync<List<ChangeLogEntry>>("/api/change-log");
        Assert.Equal(["Словарь: улица", "Словарь: дом", "Словарь: новый дом", "Словарь: улица"], log!.Take(4).Select(e => e.Action));
        Assert.Equal("Удалён", log![1].Changes);
        Assert.Contains("Квартир: — → 144", log[2].Changes);
    }

    [Fact]
    public async Task HouseWithSubscribers_IsNotDeleted()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var id = await app.WithDbAsync(db => db.Houses.Where(h => h.Subscribers.Any()).Select(h => h.Id).FirstAsync());

        var error = await ErrorAsync(await engineer.DeleteAsync($"/api/houses/{id}"));

        Assert.StartsWith("У дома есть абоненты", error);
        Assert.EndsWith("Снимите отметку «На обслуживании».", error);
        Assert.True(await app.WithDbAsync(db => db.Houses.AnyAsync(h => h.Id == id)));
    }

    [Fact]
    public async Task EditHouse_IsLogged_AndDuplicateNumberRejected()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var house = await app.WithDbAsync(db => db.Houses.Include(h => h.Street).SingleAsync(h => h.Street.Name == "Ленина"));
        (await engineer.PostAsJsonAsync("/api/houses", House(house.StreetId, "46"))).EnsureSuccessStatusCode();
        var other = await app.WithDbAsync(db => db.Houses.SingleAsync(h => h.StreetId == house.StreetId && h.Number == "46"));
        var request = House(house.StreetId, "44") with { Floors = house.Floors + 1 };

        (await engineer.PutAsJsonAsync($"/api/houses/{house.Id}", request)).EnsureSuccessStatusCode();
        var duplicate = await engineer.PutAsJsonAsync($"/api/houses/{other.Id}", request);

        Assert.Equal($"Дом 44 на улице Ленина уже есть.", await ErrorAsync(duplicate));
        var entry = (await (await app.LoginAsync("admin")).GetFromJsonAsync<List<ChangeLogEntry>>("/api/change-log"))!.First();
        Assert.Equal(("Словарь: дом", "ул. Ленина, д. 44"), (entry.Action, entry.Target));
        Assert.Contains($"Этажей: {house.Floors} → {house.Floors + 1}", entry.Changes);
    }

    [Fact]
    public async Task MaterialAndMechanic_AddEditDelete()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");

        (await engineer.PostAsJsonAsync("/api/materials", new MaterialRequest("Разветвитель 4 вых.", "шт"))).EnsureSuccessStatusCode();
        (await engineer.PostAsJsonAsync("/api/mechanics", new MechanicRequest("Петров Олег Ильич", "Радиомеханик 3 р.", "8-900-000-0000", ""))).EnsureSuccessStatusCode();
        var materialId = await app.WithDbAsync(db => db.Materials.Where(m => m.Name == "Разветвитель 4 вых.").Select(m => m.Id).SingleAsync());
        var mechanicId = await app.WithDbAsync(db => db.Mechanics.Where(m => m.FullName == "Петров Олег Ильич").Select(m => m.Id).SingleAsync());

        (await engineer.PutAsJsonAsync($"/api/materials/{materialId}", new MaterialRequest("Разветвитель 4 вых.", "компл"))).EnsureSuccessStatusCode();
        Assert.Equal("компл", await app.WithDbAsync(db => db.Materials.Where(m => m.Id == materialId).Select(m => m.Unit).SingleAsync()));
        Assert.Equal("Наименование материала — от 1 до 200 символов.",
            await ErrorAsync(await engineer.PutAsJsonAsync($"/api/materials/{materialId}", new MaterialRequest(" ", "шт"))));

        (await engineer.DeleteAsync($"/api/materials/{materialId}")).EnsureSuccessStatusCode();
        (await engineer.DeleteAsync($"/api/mechanics/{mechanicId}")).EnsureSuccessStatusCode();
        Assert.False(await app.WithDbAsync(db => db.Materials.AnyAsync(m => m.Id == materialId)));
        Assert.False(await app.WithDbAsync(db => db.Mechanics.AnyAsync(m => m.Id == mechanicId)));
    }

    [Fact]
    public async Task Dispatcher_CannotChangePhonebook()
    {
        await using var app = new AversApp();
        var dispatcher = await app.LoginAsync("orlova");

        var street = await dispatcher.PostAsJsonAsync("/api/streets", new StreetRequest("Новая"));
        var mechanic = await dispatcher.DeleteAsync("/api/mechanics/1");

        Assert.Equal(HttpStatusCode.Forbidden, street.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mechanic.StatusCode);
    }

    private sealed record Problem(Dictionary<string, string[]> Errors);
}
