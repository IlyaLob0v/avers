using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Subscribers;

public static class SubscriberEndpoints
{
    public const int MaxResults = 50;

    public static void MapSubscribers(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Абоненты").RequireAuthorization(Policies.SubscribersView);

        api.MapGet("/streets", Streets);
        api.MapGet("/streets/{streetId:int}/houses", Houses);
        api.MapGet("/subscribers", Search);
        api.MapGet("/subscribers/{id:int}", Card);
        api.MapGet("/subscribers/{id:int}/payments", Payments);
        api.MapPost("/subscribers/{id:int}/payments", PaymentEndpoints.Record).RequireAuthorization(Policies.PaymentsCreate);
    }

    private static async Task<Ok<List<StreetDto>>> Streets(AversDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Streets.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new StreetDto(s.Id, s.Name)).ToListAsync(ct));

    private static async Task<Ok<List<HouseDto>>> Houses(int streetId, AversDbContext db, CancellationToken ct)
    {
        var houses = await db.Houses.AsNoTracking().Include(h => h.Street)
            .Where(h => h.StreetId == streetId).ToListAsync(ct);
        return TypedResults.Ok(houses.OrderBy(h => h.Number, HouseNumberComparer.Instance)
            .Select(h => new HouseDto(h.Id, h.StreetId, h.Number, h.AddressLine)).ToList());
    }

    /// <summary>
    /// Поиск как на «Рабочем столе» оболочки: <c>mode</c> = address (street, house, apartment),
    /// account, innerCode, pzhreu, surname (от 4 букв). Не больше <see cref="MaxResults"/> строк.
    /// </summary>
    private static async Task<Results<Ok<List<SubscriberListItem>>, ValidationProblem>> Search(
        AversDbContext db, CancellationToken ct,
        SearchMode mode = SearchMode.Address, string? q = null,
        string? street = null, string? house = null, string? apartment = null)
    {
        var text = (q ?? "").Trim().ToLower();
        var query = db.Subscribers.AsNoTracking();

        switch (mode)
        {
            case SearchMode.Address:
                var streetName = (street ?? "").Trim().ToLower();
                var houseNumber = (house ?? "").Trim().ToLower();
                if (streetName.Length == 0 || houseNumber.Length == 0)
                    return Invalid("street", "Для поиска по адресу нужны улица и дом.");
                query = query.Where(s => s.House.Street.Name.ToLower().StartsWith(streetName) && s.House.Number.ToLower() == houseNumber);
                if (!string.IsNullOrWhiteSpace(apartment))
                {
                    var apt = apartment.Trim();
                    query = query.Where(s => s.Apartment == apt);
                }
                break;
            case SearchMode.Surname:
                if (text.Length < 4)
                    return Invalid("q", "Фамилию ищем от 4 букв.");
                query = query.Where(s => s.LastName.ToLower().StartsWith(text));
                break;
            default:
                if (text.Length == 0)
                    return Invalid("q", "Введите, что искать.");
                query = mode switch
                {
                    SearchMode.Account => query.Where(s => s.Account.ToLower().Contains(text)),
                    SearchMode.InnerCode => query.Where(s => s.InnerCode.ToLower().Contains(text)),
                    _ => query.Where(s => s.PzhreuIndex.ToLower().Contains(text) || s.PzhreuAccount.ToLower().Contains(text))
                };
                break;
        }

        var found = await query
            .Include(s => s.House).ThenInclude(h => h.Street)
            .Include(s => s.Payments)
            .OrderBy(s => s.LastName).ThenBy(s => s.Id)
            .Take(MaxResults)
            .AsSplitQuery()
            .ToListAsync(ct);

        return TypedResults.Ok(found.Select(s => new SubscriberListItem(
            s.Id, s.Account, s.InnerCode, s.FullName, s.HouseId, s.House.AddressLine, s.Apartment,
            Billing.Debt(s.Payments))).ToList());

        static ValidationProblem Invalid(string field, string message) =>
            TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
    }

    private static async Task<Results<Ok<SubscriberCard>, NotFound>> Card(
        int id, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var s = await db.Subscribers.AsNoTracking()
            .Include(x => x.House).ThenInclude(h => h.Street)
            .Include(x => x.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return TypedResults.NotFound();

        var debt = Billing.Debt(s.Payments);
        return TypedResults.Ok(new SubscriberCard(
            s.Id, s.Account, s.InnerCode, s.LastName, s.FirstName, s.Patronymic, s.FullName, s.Phone,
            s.HouseId, s.House.AddressLine, s.Apartment, s.Section,
            s.ContractDate, s.ContractTerminated, s.Notes, s.PzhreuIndex, s.PzhreuAccount, s.Benefit,
            s.LastPaymentDate,
            Billing.Charged(s.Payments), Billing.Paid(s.Payments), debt, Billing.UnpaidMonths(s.Payments),
            debt >= Billing.DebtHighlight, Billing.IsOldPayer(s.LastPaymentDate, time.Today())));
    }

    private static async Task<Results<Ok<List<PaymentDto>>, NotFound>> Payments(
        int id, AversDbContext db, CancellationToken ct, int? year = null)
    {
        if (!await db.Subscribers.AnyAsync(s => s.Id == id, ct))
            return TypedResults.NotFound();

        var query = db.Payments.AsNoTracking().Where(p => p.SubscriberId == id);
        if (year is not null)
            query = query.Where(p => p.Year == year);
        var list = await query.OrderBy(p => p.Year).ThenBy(p => p.Month).ToListAsync(ct);
        return TypedResults.Ok(list.Select(PaymentEndpoints.ToDto).ToList());
    }
}

/// <summary>Номера домов по-человечески: 2, 9, 10, 10а, 12/1.</summary>
internal sealed class HouseNumberComparer : IComparer<string>
{
    public static readonly HouseNumberComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var (nx, rx) = Split(x ?? "");
        var (ny, ry) = Split(y ?? "");
        var byNumber = nx.CompareTo(ny);
        return byNumber != 0 ? byNumber : string.Compare(rx, ry, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Number, string Suffix) Split(string s)
    {
        var digits = s.TakeWhile(char.IsDigit).Count();
        var number = digits > 0 && int.TryParse(s.AsSpan(0, Math.Min(digits, 9)), out var n) ? n : int.MaxValue;
        return (number, s[digits..]);
    }
}
