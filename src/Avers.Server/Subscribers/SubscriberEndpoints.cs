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
        api.MapGet("/houses/{houseId:int}/subscribers", (int houseId, AversDbContext db) => TypedResults.Ok(HouseSubscribers.Load(db, houseId)));
        api.MapGet("/debts", (AversDbContext db, TimeProvider time) => TypedResults.Ok(Debts.Load(db, time.Today())));
        api.MapGet("/reports/accounting", Accounting);
        api.MapGet("/subscribers/{id:int}", Card);
        api.MapGet("/subscribers/{id:int}/payments", Payments);
        api.MapPost("/subscribers/{id:int}/payments", PaymentEndpoints.Record).RequireAuthorization(Policies.PaymentsCreate);
        api.MapDelete("/subscribers/{id:int}/payments/{paymentId:int}", PaymentEndpoints.Cancel).RequireAuthorization(Policies.PaymentsCancel);
        api.MapPost("/subscribers", ApartmentEndpoints.Add).RequireAuthorization(Policies.SubscribersEdit);
        api.MapPut("/subscribers/{id:int}/contract", ApartmentEndpoints.UpdateContract).RequireAuthorization(Policies.SubscribersEdit);
        api.MapGet("/settings/payment-warning", ApartmentEndpoints.GetPaymentWarning);
        api.MapPut("/settings/payment-warning", ApartmentEndpoints.SetPaymentWarning).RequireAuthorization(Policies.SettingsEdit);
        api.MapPut("/tariff", ApartmentEndpoints.ChangeTariff).RequireAuthorization(Policies.SettingsEdit);
        api.MapPost("/accruals/{year:int}", ApartmentEndpoints.OpenYear).RequireAuthorization(Policies.SettingsEdit);
    }

    private static async Task<Ok<List<StreetDto>>> Streets(AversDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Streets.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new StreetDto(s.Id, s.Name)).ToListAsync(ct));

    private static async Task<Ok<List<HouseDto>>> Houses(int streetId, AversDbContext db, CancellationToken ct)
    {
        var houses = await db.Houses.AsNoTracking().Include(h => h.Street)
            .Where(h => h.StreetId == streetId).ToListAsync(ct);
        return TypedResults.Ok(houses.OrderBy(h => h.Number, NaturalOrder.Instance)
            .Select(h => new HouseDto(h.Id, h.StreetId, h.Number, h.AddressLine)).ToList());
    }

    /// <summary>
    /// Поиск как на «Рабочем столе» оболочки: <c>mode</c> = address (street, house, apartment),
    /// account, innerCode, pzhreu, surname (короче 4 букв — фамилия целиком). Не больше <see cref="MaxResults"/> строк.
    /// </summary>
    private static async Task<Results<Ok<List<SubscriberListItem>>, ValidationProblem>> Search(
        AversDbContext db, TimeProvider time, CancellationToken ct,
        SearchMode mode = SearchMode.Address, string? q = null,
        string? street = null, string? house = null, string? apartment = null)
    {
        var (query, field, error) = SubscriberSearch.Where(db.Subscribers.AsNoTracking(), mode, q, street, house, apartment);
        if (query is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [error] });

        var today = time.Today();
        var dueMonth = today.Year * 12 + today.Month;
        var found = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.Id)
            .Take(MaxResults)
            .Select(s => new
            {
                Subscriber = s,
                s.House,
                Street = s.House.Street,
                Charged = s.Payments.Where(p => p.Year * 12 + p.Month <= dueMonth).Sum(p => p.Tariff),
                Paid = s.Payments.Where(p => p.Year * 12 + p.Month <= dueMonth).Sum(p => p.Amount ?? 0)
            })
            .ToListAsync(ct);

        // Проекция не связывает дом с улицей, а адрес строится по улице.
        foreach (var x in found)
            x.House.Street = x.Street;
        return TypedResults.Ok(found.Select(x => new SubscriberListItem(
            x.Subscriber.Id, x.Subscriber.Account, x.Subscriber.InnerCode, x.Subscriber.FullName, x.Subscriber.HouseId,
            x.House.AddressLine, x.Subscriber.Apartment, Math.Max(0, x.Charged - x.Paid))).ToList());
    }

    private static Results<Ok<AccountingReport>, ValidationProblem> Accounting(AversDbContext db, int year, int month) =>
        month is >= 1 and <= 12 && year is >= 2000 and <= 2100
            ? TypedResults.Ok(AccountingReport.Load(db, year, month))
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Неверный месяц."] });

    private static async Task<Results<Ok<SubscriberCard>, NotFound>> Card(
        int id, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var s = await db.Subscribers.AsNoTracking()
            .Include(x => x.House).ThenInclude(h => h.Street)
            .Include(x => x.House).ThenInclude(h => h.Organization)
            .Include(x => x.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return TypedResults.NotFound();

        var today = time.Today();
        var due = Billing.Due(s.Payments, today);
        var debt = Billing.Debt(due);
        var warning = Settings.PaymentWarningMonths(db);
        var last = PaymentAge.LastPayment(s.Payments);
        return TypedResults.Ok(new SubscriberCard(
            s.Id, s.Account, s.InnerCode, s.LastName, s.FirstName, s.Patronymic, s.FullName, s.Phone,
            s.HouseId, s.House.AddressLine, s.Apartment, s.Section,
            s.ContractDate, s.ContractTerminated, s.Notes, s.PzhreuIndex, s.PzhreuAccount, s.Benefit,
            s.LastPaymentDate,
            Billing.Charged(due), Billing.Paid(due), debt, Billing.UnpaidMonths(due),
            debt >= Billing.DebtHighlight, Billing.IsOldPayer(s.LastPaymentDate, today),
            s.ContractNumber, s.ContractParty, ServiceOrganizations.Title(s.House.Organization),
            last is null ? null : PaymentEndpoints.ToDto(last),
            PaymentAge.MonthsSince(s.LastPaymentDate, today),
            PaymentAge.IsLate(s, today, warning),
            warning));
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
