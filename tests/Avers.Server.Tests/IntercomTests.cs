using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class IntercomTests
{
    private static Task<int> HouseIdAsync(AversApp app) =>
        app.WithDbAsync(db => db.Houses.Where(h => h.Street.Name == "Волочаевская" && h.Number == "19Д").Select(h => h.Id).SingleAsync());

    [Fact]
    public async Task Engineer_AddsAndEditsPanel_Logged()
    {
        await using var app = new AversApp();
        var engineer = await app.LoginAsync("engineer");
        var house = await HouseIdAsync(app);

        (await engineer.PostAsJsonAsync($"/api/houses/{house}/intercoms", new IntercomPanelRequest(1, "Vizit", "", "", ""))).EnsureSuccessStatusCode();
        var panel = await app.WithDbAsync(db => db.IntercomPanels.SingleAsync(p => p.HouseId == house && p.Entrance == 1));
        (await engineer.PutAsJsonAsync($"/api/houses/{house}/intercoms/{panel.Id}", new IntercomPanelRequest(1, "Vizit", "", "", "Щит под. 1"))).EnsureSuccessStatusCode();
        var duplicate = await engineer.PostAsJsonAsync($"/api/houses/{house}/intercoms", new IntercomPanelRequest(2, "", "", "", ""));

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var entry = (await (await app.LoginAsync("admin")).GetFromJsonAsync<List<ChangeLogEntry>>("/api/change-log"))!.First();
        Assert.Equal(("Домофон: панель", "ул. Волочаевская, д. 19Д", "Питание: — → Щит под. 1"), (entry.Action, entry.Target, entry.Changes));
    }

    [Fact]
    public async Task Dispatcher_AcceptsAndClosesTicket()
    {
        await using var app = new AversApp();
        var dispatcher = await app.LoginAsync("orlova");
        var house = await HouseIdAsync(app);

        (await dispatcher.PostAsJsonAsync($"/api/houses/{house}/intercom-tickets", new IntercomTicketRequest(2, "Не работает трубка кв. 70", "Ершов Павел Юрьевич"))).EnsureSuccessStatusCode();
        var ticket = await app.WithDbAsync(db => db.IntercomTickets.SingleAsync(t => t.Fault == "Не работает трубка кв. 70"));
        Assert.Equal((AversApp.Today, false), (ticket.Date, ticket.Done));

        (await dispatcher.PutAsync($"/api/intercom-tickets/{ticket.Id}/done", null)).EnsureSuccessStatusCode();
        Assert.True(await app.WithDbAsync(db => db.IntercomTickets.Where(t => t.Id == ticket.Id).Select(t => t.Done).SingleAsync()));
    }

    [Fact]
    public async Task Rights_PanelsForHouseEditors_TicketsForAds()
    {
        await using var app = new AversApp();
        var house = await HouseIdAsync(app);
        var dispatcher = await app.LoginAsync("orlova");
        var engineer = await app.LoginAsync("engineer");

        var panel = await dispatcher.PostAsJsonAsync($"/api/houses/{house}/intercoms", new IntercomPanelRequest(1, "", "", "", ""));
        var ticket = await engineer.PostAsJsonAsync($"/api/houses/{house}/intercom-tickets", new IntercomTicketRequest(1, "Нет связи", ""));

        Assert.Equal(HttpStatusCode.Forbidden, panel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ticket.StatusCode);
    }
}
