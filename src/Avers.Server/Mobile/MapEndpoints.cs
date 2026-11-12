using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Mobile;

/// <summary>
/// Офлайн-карта города: файл карты (PMTiles, OpenStreetMap) отдаётся по частям, точки домов — из базы.
/// Страница — <c>wwwroot/m/</c>, раздел «Карта». Интернет для работы не нужен.
/// </summary>
public static class MapEndpoints
{
    public const string DefaultMapFile = "map/city.pmtiles";

    public static void MapMap(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/mobile/map").WithTags("Карта").RequireAuthorization(Policies.HousesView);

        api.MapGet("/tiles", Tiles);
        api.MapGet("/houses", Houses);
        api.MapPut("/houses/{id:int}", Place).RequireAuthorization(Policies.HousesEdit);
    }

    private static Results<PhysicalFileHttpResult, NotFound> Tiles(IConfiguration config, IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, config["Avers:MapFile"] ?? DefaultMapFile);
        return File.Exists(path)
            ? TypedResults.PhysicalFile(path, "application/octet-stream", enableRangeProcessing: true)
            : TypedResults.NotFound();
    }

    private static async Task<Ok<MapHousesDto>> Houses(ClaimsPrincipal user, AversDbContext db, CancellationToken ct)
    {
        var houses = await db.Houses.AsNoTracking().Include(h => h.Street).ToListAsync(ct);
        var canPlace = user.Role() is { } role && Policies.HasAccess(role, AppModule.Anten, RoleCatalog.AccessLevel.Edit);
        return TypedResults.Ok(new MapHousesDto(canPlace, houses
            .OrderBy(h => h.Street.Name).ThenBy(h => h.Number, NaturalOrder.Instance)
            .Select(h => new MapHouseDto(h.Id, h.AddressLine, h.Latitude, h.Longitude, h.OpenFault is { Length: > 0 }))
            .ToList()));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> Place(
        int id, MapPointDto point, ClaimsPrincipal user, AversDbContext db, TimeProvider time)
    {
        if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["point"] = ["Точка за пределами карты."] });
        var house = await db.Houses.Include(h => h.Street).FirstOrDefaultAsync(h => h.Id == id);
        if (house is null)
            return TypedResults.NotFound();

        ChangeLog.Add(db, time.GetUtcNow(), ChangeLog.Who(db, user.AppUserId()), "Карта", house.AddressLine,
            [house.Latitude is null ? "Дом поставлен на карту" : "Дом перенесён на карте"]);
        (house.Latitude, house.Longitude) = (point.Latitude, point.Longitude);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}
