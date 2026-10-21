using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Avers.Server.Ppr;

/// <summary>График ППР правит тот же круг, что и паспорт дома: главный инженер и администратор.</summary>
public static class PprEndpoints
{
    public static void MapPpr(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/ppr").WithTags("Графики ППР").RequireAuthorization(Policies.HousesEdit);

        api.MapPut("/{houseId:int}/{year:int}/{month:int}", Mark);
        api.MapPost("/{year:int}/copy", Copy);
    }

    private static Results<NoContent, NotFound, ValidationProblem> Mark(
        int houseId, int year, int month, PprMarkRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2100)
            return Invalid("Месяц — от 1 до 12, год — от 2000 до 2100.");
        if (!Enum.TryParse<PprStatus>(request.Status, out var status) || status == PprStatus.Overdue)
            return Invalid("Статус — Empty, Planned или Done.");
        if (!db.Houses.Any(h => h.Id == houseId))
            return TypedResults.NotFound();

        PprPlan.Mark(db, houseId, year, month, status, time.Today(), ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static Results<Ok<PprCopyResult>, ProblemHttpResult> Copy(int year, ClaimsPrincipal user, AversDbContext db, TimeProvider time)
    {
        var copied = PprPlan.CopyToNextYear(db, year, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return copied > 0
            ? TypedResults.Ok(new PprCopyResult(year + 1, copied))
            : TypedResults.Problem($"План на {year + 1} год уже есть или план {year} года пуст.",
                statusCode: StatusCodes.Status409Conflict, title: "План не скопирован");
    }

    private static ValidationProblem Invalid(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ppr"] = [message] });
}
