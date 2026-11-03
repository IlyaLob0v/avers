using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Domain;

namespace Avers.Server.Tests;

public class AccessLogTests
{
    [Fact]
    public async Task ViewsOfPersonalData_AreLogged_ForAdministratorOnly()
    {
        await using var app = new AversApp();
        var orlov = await app.LoginAsync("orlov");
        var subscriber = (await orlov.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Account&q=1932068"))!.Single();

        await orlov.GetAsync($"/api/houses/{subscriber.HouseId}/subscribers");
        await orlov.GetAsync($"/api/subscribers/{subscriber.Id}");
        await orlov.GetAsync("/api/debts");
        await orlov.GetAsync("/api/streets");

        Assert.Equal(HttpStatusCode.Forbidden, (await orlov.GetAsync("/api/access-log")).StatusCode);
        var log = (await (await app.LoginAsync("admin")).GetFromJsonAsync<List<AccessLogEntry>>("/api/access-log"))!;

        Assert.All(log, e => Assert.Equal("Орлов С.А. (orlov)", e.UserName));
        Assert.Equal(
            ["Список должников", "Карточка абонента", "Абоненты дома", "Поиск абонентов"],
            log.Select(e => e.Action));
        Assert.Contains("1932068", log[1].Target);
        Assert.Equal(subscriber.Address, log[2].Target);
        Assert.Contains("1932068", log[3].Target);
    }
}
