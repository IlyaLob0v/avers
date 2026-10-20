using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Avers.Server.Desktop;

/// <summary>
/// Домофоны: панели подъездов правит тот, кто правит паспорт дома; заявки по домофону принимает и закрывает тот,
/// кто принимает заявки АДС.
/// </summary>
public static class IntercomEndpoints
{
    public static void MapIntercoms(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Домофоны");

        api.MapPost("/houses/{id:int}/intercoms", (int id, IntercomPanelRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
            SavePanel(id, 0, r, u, db, t)).RequireAuthorization(Policies.HousesEdit);
        api.MapPut("/houses/{id:int}/intercoms/{panelId:int}", SavePanel).RequireAuthorization(Policies.HousesEdit);

        api.MapPost("/houses/{id:int}/intercom-tickets", (int id, IntercomTicketRequest r, AversDbContext db, TimeProvider t) =>
            Result(Intercoms.AddTicket(db, id, new IntercomTicket { Entrance = r.Entrance, Fault = r.Fault ?? "", Mechanic = r.Mechanic ?? "" }, t.Today())))
            .RequireAuthorization(Policies.TicketsCreate);
        api.MapPut("/intercom-tickets/{id:int}/done", (int id, AversDbContext db) => Result(Intercoms.MarkDone(db, id)))
            .RequireAuthorization(Policies.TicketsCreate);
    }

    private static Results<NoContent, ValidationProblem> SavePanel(
        int id, int panelId, IntercomPanelRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
        Result(Intercoms.SavePanel(db, id, new IntercomPanel
        {
            Id = panelId,
            Entrance = r.Entrance,
            PanelType = r.PanelType ?? "",
            SwitchBlock = r.SwitchBlock ?? "",
            Keys = r.Keys ?? "",
            Power = r.Power ?? ""
        }, ChangeLog.Who(db, u.AppUserId()), t.GetUtcNow()));

    private static Results<NoContent, ValidationProblem> Result(string? error) => error is null
        ? TypedResults.NoContent()
        : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["intercom"] = [error] });
}
