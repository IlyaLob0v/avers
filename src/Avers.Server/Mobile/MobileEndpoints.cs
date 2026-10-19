using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Desktop;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Mobile;

/// <summary>
/// Веб-версия для радиомехаников (REQ-MECH-01/02): поиск дома по улице и номеру и сведения о нём, только чтение.
/// Страница — <c>wwwroot/m/index.html</c>.
/// </summary>
public static class MobileEndpoints
{
    public const int MaxResults = 30;

    public static void MapMobile(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/mobile").WithTags("Телефон механика").RequireAuthorization(Policies.HousesView);

        api.MapGet("/houses", Search);
        api.MapGet("/houses/{id:int}", Get);
        api.MapGet("/schemes/{id:int}", SchemeEndpoints.File);
    }

    private static async Task<Ok<List<HouseDto>>> Search(
        AversDbContext db, CancellationToken ct, string? street = null, string? house = null)
    {
        var streetName = (street ?? "").Trim().ToLower();
        var number = (house ?? "").Trim().ToLower();
        if (streetName.Length == 0)
            return TypedResults.Ok(new List<HouseDto>());

        var houses = await db.Houses.AsNoTracking().Include(h => h.Street)
            .Where(h => h.Street.Name.ToLower().StartsWith(streetName) && h.Number.ToLower().StartsWith(number))
            .ToListAsync(ct);
        return TypedResults.Ok(houses
            .OrderBy(h => h.Street.Name).ThenBy(h => h.Number, NaturalOrder.Instance)
            .Take(MaxResults)
            .Select(h => new HouseDto(h.Id, h.StreetId, h.Number, h.AddressLine)).ToList());
    }

    private static async Task<Results<Ok<MobileHouseDto>, NotFound>> Get(int id, AversDbContext db, CancellationToken ct)
    {
        var h = await db.Houses.AsNoTracking().Include(x => x.Street).Include(x => x.Organization).Include(x => x.Schemes)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (h is null)
            return TypedResults.NotFound();

        var schemeIds = h.Schemes.Select(s => s.Id).ToList();
        var withImage = await db.HouseSchemeFiles.Where(f => schemeIds.Contains(f.HouseSchemeId)).Select(f => f.HouseSchemeId).ToListAsync(ct);
        var org = h.Organization ?? new Organization();
        return TypedResults.Ok(new MobileHouseDto(
            h.Id, h.AddressLine, h.Floors, h.Entrances, h.Apartments,
            org.Name, org.Branch, org.Address, org.Phones, org.HeadName, org.HeadPhone, org.EngineerName, org.EngineerPhone,
            h.Antennas, h.AerialCables, h.Amplifiers, h.PowerSupplies, h.TechFloorKey, h.OtherNotes,
            h.OpenFault, h.OpenFaultReason, h.OpenFaultPlannedFix,
            h.Schemes.OrderBy(s => s.Name).Select(s => new MobileSchemeDto(s.Id, s.Name, s.Format, s.UpdatedAt, withImage.Contains(s.Id))).ToList()));
    }
}
