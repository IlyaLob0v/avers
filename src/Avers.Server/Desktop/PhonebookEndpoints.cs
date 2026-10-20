using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Avers.Server.Desktop;

/// <summary>Словарь: улицы, дома, материалы и радиомеханики правят администратор и главный инженер.</summary>
public static class PhonebookEndpoints
{
    public static void MapPhonebook(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Словарь").RequireAuthorization(Policies.PhonebookEdit);

        api.MapPost("/streets", (StreetRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) => SaveStreet(0, r, u, db, t));
        api.MapPut("/streets/{id:int}", SaveStreet);
        api.MapDelete("/streets/{id:int}", (int id, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
            Result(Phonebook.DeleteStreet(db, id, Who(db, u), t.GetUtcNow())));

        api.MapPost("/houses", (HouseRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) => SaveHouse(0, r, u, db, t));
        api.MapPut("/houses/{id:int}", SaveHouse);
        api.MapDelete("/houses/{id:int}", (int id, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
            Result(Phonebook.DeleteHouse(db, id, Who(db, u), t.GetUtcNow())));

        api.MapPost("/materials", (MaterialRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) => SaveMaterial(0, r, u, db, t));
        api.MapPut("/materials/{id:int}", SaveMaterial);
        api.MapDelete("/materials/{id:int}", (int id, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
            Result(Phonebook.DeleteMaterial(db, id, Who(db, u), t.GetUtcNow())));

        api.MapPost("/mechanics", (MechanicRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) => SaveMechanic(0, r, u, db, t));
        api.MapPut("/mechanics/{id:int}", SaveMechanic);
        api.MapDelete("/mechanics/{id:int}", (int id, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
            Result(Phonebook.DeleteMechanic(db, id, Who(db, u), t.GetUtcNow())));
    }

    private static Results<NoContent, ValidationProblem> SaveStreet(int id, StreetRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
        Result(Phonebook.SaveStreet(db, new Street { Id = id, Name = r.Name ?? "" }, Who(db, u), t.GetUtcNow()));

    private static Results<NoContent, ValidationProblem> SaveHouse(int id, HouseRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
        Result(Phonebook.SaveHouse(db, new House
        {
            Id = id,
            StreetId = r.StreetId,
            Number = r.Number ?? "",
            PzhreuIndex = r.PzhreuIndex ?? "",
            PzhreuAccount = r.PzhreuAccount ?? "",
            Floors = r.Floors,
            Entrances = r.Entrances,
            Risers = r.Risers,
            Apartments = r.Apartments,
            Zone = r.Zone ?? "",
            OrganizationId = r.OrganizationId,
            Serviced = r.Serviced
        }, Who(db, u), t.GetUtcNow()));

    private static Results<NoContent, ValidationProblem> SaveMaterial(int id, MaterialRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
        Result(Phonebook.SaveMaterial(db, new Material { Id = id, Name = r.Name ?? "", Unit = r.Unit ?? "" }, Who(db, u), t.GetUtcNow()));

    private static Results<NoContent, ValidationProblem> SaveMechanic(int id, MechanicRequest r, ClaimsPrincipal u, AversDbContext db, TimeProvider t) =>
        Result(Phonebook.SaveMechanic(db, new Mechanic
        {
            Id = id,
            FullName = r.FullName ?? "",
            Position = r.Position ?? "",
            Phone = r.Phone ?? "",
            Address = r.Address ?? ""
        }, Who(db, u), t.GetUtcNow()));

    private static string Who(AversDbContext db, ClaimsPrincipal user) => ChangeLog.Who(db, user.AppUserId());

    private static Results<NoContent, ValidationProblem> Result(string? error) => error is null
        ? TypedResults.NoContent()
        : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["phonebook"] = [error] });
}
