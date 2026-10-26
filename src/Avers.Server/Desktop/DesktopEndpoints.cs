using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
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
        var o = request.Organization;
        var fields = new[] { request.Antennas, request.AerialCables, request.Amplifiers, request.PowerSupplies, request.TechFloorKey, request.OtherNotes,
            request.OpenFault, request.OpenFaultReason };
        if (fields.Any(f => f is { Length: > 1000 }))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["passport"] = ["Поле паспорта — не длиннее 1000 символов."] });
        if (o is not null && (string.IsNullOrWhiteSpace(o.Name) || new[] { o.Name, o.Branch, o.Address, o.Phones, o.HeadName, o.EngineerName }.Any(f => f is { Length: > 200 })))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["organization"] = ["Название организации обязательно, поля — не длиннее 200 символов."] });

        var draft = new House
        {
            Antennas = request.Antennas ?? "",
            AerialCables = request.AerialCables ?? "",
            Amplifiers = request.Amplifiers ?? "",
            PowerSupplies = request.PowerSupplies ?? "",
            TechFloorKey = request.TechFloorKey ?? "",
            OtherNotes = request.OtherNotes ?? "",
            OpenFault = request.OpenFault,
            OpenFaultReason = request.OpenFaultReason,
            OpenFaultPlannedFix = request.OpenFaultPlannedFix,
            Organization = o is not null
                ? new Organization { Name = o.Name ?? "", Branch = o.Branch ?? "", Address = o.Address ?? "", Phones = o.Phones ?? "", HeadName = o.HeadName ?? "", EngineerName = o.EngineerName ?? "" }
                : null
        };
        if (!HousePassport.Save(db, id, draft, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow()))
            return TypedResults.NotFound();
        return TypedResults.NoContent();
    }
}
