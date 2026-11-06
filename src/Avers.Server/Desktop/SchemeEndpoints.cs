using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using AccessLevel = Avers.Domain.RoleCatalog.AccessLevel;

namespace Avers.Server.Desktop;

/// <summary>Картинки схем СКПТ: смотрит тот, кому открыт модуль схем, загружает — кто его правит.</summary>
public static class SchemeEndpoints
{
    public static void MapSchemes(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Схемы СКПТ");

        api.MapGet("/schemes/{id:int}/file", File).RequireAuthorization(Policies.SchemesScreen);
        api.MapPost("/houses/{id:int}/schemes", Add).RequireAuthorization(Policies.SchemesEdit);
        api.MapPut("/schemes/{id:int}/file", Replace).RequireAuthorization(Policies.SchemesEdit);
        api.MapPut("/schemes/{id:int}/name", Rename).RequireAuthorization(Policies.SchemesEdit);
        api.MapDelete("/schemes/{id:int}", Delete).RequireAuthorization(Policies.SchemesEdit);
    }

    /// <summary>Исходный файл — тому, кому схемы открыты на просмотр; роли «только на экране» — картинка со знаком.</summary>
    private static Task<Results<FileContentHttpResult, NotFound>> File(
        int id, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct) =>
        SendAsync(id, user, db, time, original: user.Role() is { } role && Policies.HasAccess(role, AppModule.Scheme, AccessLevel.View), ct);

    /// <summary>Телефон механика: схема всегда со знаком, кто бы ни смотрел.</summary>
    public static Task<Results<FileContentHttpResult, NotFound>> Screen(
        int id, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct) =>
        SendAsync(id, user, db, time, original: false, ct);

    private static async Task<Results<FileContentHttpResult, NotFound>> SendAsync(
        int id, ClaimsPrincipal user, AversDbContext db, TimeProvider time, bool original, CancellationToken ct)
    {
        var scheme = await db.HouseSchemeFiles.AsNoTracking().Where(f => f.HouseSchemeId == id)
            .Select(f => new { f.Content, f.Scheme.Format }).FirstOrDefaultAsync(ct);
        if (scheme is null)
            return TypedResults.NotFound();
        var content = original
            ? scheme.Content
            : SchemeWatermark.Apply(scheme.Content, scheme.Format, SchemeWatermark.Text(user.Identity?.Name ?? "", time.GetLocalNow()));
        return content is null
            ? TypedResults.NotFound()
            : TypedResults.File(content, SchemeFiles.ContentType(scheme.Format));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> Add(
        int id, AddSchemeRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var error = SchemeFiles.CheckName(request.Name) ?? SchemeFiles.Check(request.Content);
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

        if (await FindAsync(db, id, ct) is not { } scheme)
            return TypedResults.NotFound();

        SchemeFiles.Replace(db, scheme, request.Content, time.Today(), ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> Rename(
        int id, RenameSchemeRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (SchemeFiles.CheckName(request.Name) is { } error)
            return Invalid(error);
        if (await FindAsync(db, id, ct) is not { } scheme)
            return TypedResults.NotFound();

        SchemeFiles.Rename(db, scheme, request.Name, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        int id, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await FindAsync(db, id, ct) is not { } scheme)
            return TypedResults.NotFound();

        SchemeFiles.Delete(db, scheme, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static Task<HouseScheme?> FindAsync(AversDbContext db, int id, CancellationToken ct) =>
        db.HouseSchemes.Include(s => s.House).ThenInclude(h => h.Street).FirstOrDefaultAsync(s => s.Id == id, ct);

    private static ValidationProblem Invalid(string error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["scheme"] = [error] });
}
