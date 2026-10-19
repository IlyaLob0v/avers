using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Desktop;

/// <summary>Что нужно настольной программе сверх общего API: снимок базы после входа и паспорт дома.</summary>
public static class DesktopEndpoints
{
    public static void MapDesktop(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Настольная программа").RequireAuthorization();

        api.MapGet("/desktop/snapshot", Snapshot);
        api.MapPut("/houses/{id:int}/passport", SavePassport).RequireAuthorization(Policies.HousesEdit);
    }

    /// <summary>Таблицы без связей; абоненты и заявки — только если они положены роли.</summary>
    private static Results<Ok<AversSnapshot>, ForbidHttpResult> Snapshot(ClaimsPrincipal user, AversDbContext db) =>
        user.Role() is { } role ? TypedResults.Ok(AversSnapshot.Load(db, role)) : TypedResults.Forbid();

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> SavePassport(
        int id, HousePassportRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var fields = new[] { request.Antennas, request.AerialCables, request.Amplifiers, request.PowerSupplies, request.TechFloorKey, request.OtherNotes };
        if (fields.Any(f => f is { Length: > 1000 }))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["passport"] = ["Поле паспорта — не длиннее 1000 символов."] });

        var house = await db.Houses.Include(h => h.Street).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (house is null)
            return TypedResults.NotFound();

        var changes = ChangeLog.Diff(
            ("Антенны, подъезды", house.Antennas, request.Antennas),
            ("Воздушные линии", house.AerialCables, request.AerialCables),
            ("Усилители", house.Amplifiers, request.Amplifiers),
            ("Блоки питания", house.PowerSupplies, request.PowerSupplies),
            ("Ключ техэтажа", house.TechFloorKey, request.TechFloorKey),
            ("Прочее", house.OtherNotes, request.OtherNotes));

        house.Antennas = request.Antennas?.Trim() ?? "";
        house.AerialCables = request.AerialCables?.Trim() ?? "";
        house.Amplifiers = request.Amplifiers?.Trim() ?? "";
        house.PowerSupplies = request.PowerSupplies?.Trim() ?? "";
        house.TechFloorKey = request.TechFloorKey?.Trim() ?? "";
        house.OtherNotes = request.OtherNotes?.Trim() ?? "";
        ChangeLog.Add(db, time.GetUtcNow(), ChangeLog.Who(db, user.AppUserId()), "Паспорт дома", house.AddressLine, changes);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
