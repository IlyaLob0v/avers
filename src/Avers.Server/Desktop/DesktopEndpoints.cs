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
        int id, HousePassportRequest request, AversDbContext db, CancellationToken ct)
    {
        var fields = new[] { request.Antennas, request.AerialCables, request.Amplifiers, request.PowerSupplies, request.TechFloorKey, request.OtherNotes };
        if (fields.Any(f => f is { Length: > 1000 }))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["passport"] = ["Поле паспорта — не длиннее 1000 символов."] });

        var house = await db.Houses.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (house is null)
            return TypedResults.NotFound();

        house.Antennas = request.Antennas?.Trim() ?? "";
        house.AerialCables = request.AerialCables?.Trim() ?? "";
        house.Amplifiers = request.Amplifiers?.Trim() ?? "";
        house.PowerSupplies = request.PowerSupplies?.Trim() ?? "";
        house.TechFloorKey = request.TechFloorKey?.Trim() ?? "";
        house.OtherNotes = request.OtherNotes?.Trim() ?? "";
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
