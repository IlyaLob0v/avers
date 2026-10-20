using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;

namespace Avers.Server.Tests;

public class MobileTests
{
    [Fact]
    public async Task Mechanic_FindsHouseByStreetAndNumber()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("novikov");

        var houses = await client.GetFromJsonAsync<List<HouseDto>>("/api/mobile/houses?street=вол&house=19");

        Assert.Equal(["ул. Волочаевская, д. 19", "ул. Волочаевская, д. 19А", "ул. Волочаевская, д. 19Д"],
            houses!.Select(h => h.Address));
    }

    [Fact]
    public async Task Mechanic_SeesHomeownerTechnicsAndSchemes()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("novikov");
        var id = (await client.GetFromJsonAsync<List<HouseDto>>("/api/mobile/houses?street=Ленина&house=44"))!.Single().Id;

        var house = await client.GetFromJsonAsync<MobileHouseDto>($"/api/mobile/houses/{id}");

        Assert.NotEqual("", house!.Homeowner);
        Assert.NotEqual("", house.HomeownerPhones);
        Assert.Equal("У старшей по дому, кв. 7.", house.TechFloorKey);
        Assert.Equal("Схема СКПТ Ленина 44", house.Schemes.Single().Name);
        Assert.True(house.Schemes.Single().HasImage);
    }

    [Fact]
    public async Task Lawyer_HasNoAccess()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("lawyer");

        var response = await client.GetAsync("/api/mobile/houses?street=вол");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Page_OpensWithoutLogin()
    {
        await using var app = new AversApp();

        var response = await app.CreateClient().GetAsync("/m/");

        response.EnsureSuccessStatusCode();
        Assert.Contains("Аверс · дома", await response.Content.ReadAsStringAsync());
    }
}
