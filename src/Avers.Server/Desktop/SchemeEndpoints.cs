using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Desktop;

/// <summary>Картинки схем СКПТ: смотрит тот, кому открыт модуль схем, загружает — кто его правит.</summary>
public static class SchemeEndpoints
{
    public static void MapSchemes(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Схемы СКПТ");

        api.MapGet("/schemes/{id:int}/file", File).RequireAuthorization(Policies.SchemesView);
        api.MapPost("/houses/{id:int}/schemes", Add).RequireAuthorization(Policies.SchemesEdit);
        api.MapPut("/schemes/{id:int}/file", Replace).RequireAuthorization(Policies.SchemesEdit);
    }

    public static async Task<Results<FileContentHttpResult, NotFound>> File(int id, AversDbContext db, CancellationToken ct)
    {
        var scheme = await db.HouseSchemeFiles.AsNoTracking().Where(f => f.HouseSchemeId == id)
            .Select(f => new { f.Content, f.Scheme.Format }).FirstOrDefaultAsync(ct);
        return scheme is null
            ? TypedResults.NotFound()
            : TypedResults.File(scheme.Content, SchemeFiles.ContentType(scheme.Format));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> Add(
        int id, AddSchemeRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var error = string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200
            ? "Название схемы — от 1 до 200 символов."
            : SchemeFiles.Check(request.Content);
        if (error is not null)
            return Invalid(error);

        var house = await db.Houses.Include(h => h.Street).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (house is null)
            return TypedResults.NotFound();

        SchemeFiles.Add(db, house, request.Name, request.Content, time.Today(), ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> Replace(
        int id, SchemeFileRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (SchemeFiles.Check(request.Content) is { } error)
            return Invalid(error);

        var scheme = await db.HouseSchemes.Include(s => s.House).ThenInclude(h => h.Street).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (scheme is null)
            return TypedResults.NotFound();

        SchemeFiles.Replace(db, scheme, request.Content, time.Today(), ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static ValidationProblem Invalid(string error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["scheme"] = [error] });
}
