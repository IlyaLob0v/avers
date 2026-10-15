using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tickets;

/// <summary>
/// Заявки АДС. Смотреть может каждый, кому открыт модуль «Заявки»; принимать — пользователь и начальник АДС;
/// править любую — начальник АДС, свою — пользователь АДС (<see cref="RoleCatalog.CanEditTicket"/>).
/// </summary>
public static class TicketEndpoints
{
    public const int MaxResults = 1000;

    public static void MapTickets(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Заявки").RequireAuthorization(Policies.TicketsView);

        api.MapGet("/tickets", List);
        api.MapGet("/tickets/by-month", ByMonth);
        api.MapGet("/tickets/{id:int}", Get);
        api.MapPost("/tickets", Create).RequireAuthorization(Policies.TicketsCreate);
        api.MapPut("/tickets/{id:int}", Update).RequireAuthorization(Policies.TicketsCreate);
        api.MapGet("/houses/{houseId:int}/tickets", ForHouse);
        api.MapGet("/houses/{houseId:int}/service-info", ServiceInfo);
        api.MapGet("/subscribers/{subscriberId:int}/tickets", ForSubscriber);
    }

    /// <summary>
    /// Журнал: <c>from</c>/<c>to</c> или <c>year</c> (+ <c>month</c>), <c>houseId</c>, <c>apartment</c>,
    /// <c>subscriberId</c>, <c>kind</c> (Antenna, Cable…), <c>status</c>, <c>open=true</c>, <c>q</c> — текст.
    /// </summary>
    private static async Task<Results<Ok<List<TicketDto>>, ValidationProblem>> List(
        ClaimsPrincipal user, AversDbContext db, CancellationToken ct,
        DateOnly? from = null, DateOnly? to = null, int? year = null, int? month = null,
        int? houseId = null, string? apartment = null, int? subscriberId = null,
        TicketKind? kind = null, TicketStatus? status = null, bool open = false, string? q = null)
    {
        if (month is not null && year is null)
            return Invalid("month", "Месяц указывается вместе с годом.");
        if (month is < 1 or > 12)
            return Invalid("month", "Месяц — от 1 до 12.");
        if (year is < 2000 or > 2100)
            return Invalid("year", "Год вне допустимого диапазона.");
        if (from is not null && to is not null && from > to)
            return Invalid("from", "Начало периода позже конца.");

        var period = year is { } y
            ? month is { } m ? TicketFilter.ForMonth(y, m) : TicketFilter.ForYear(y)
            : new TicketFilter { From = from, To = to };
        var filter = period with
        {
            HouseId = houseId,
            Apartment = apartment,
            SubscriberId = subscriberId,
            Kind = kind,
            Status = status,
            OnlyOpen = open,
            Text = q
        };
        return TypedResults.Ok(await Load(db, filter, user, ct));
    }

    private static async Task<Ok<List<TicketDto>>> ForHouse(
        int houseId, ClaimsPrincipal user, AversDbContext db, CancellationToken ct,
        DateOnly? from = null, DateOnly? to = null, TicketKind? kind = null) =>
        TypedResults.Ok(await Load(db, new TicketFilter { HouseId = houseId, From = from, To = to, Kind = kind }, user, ct));

    /// <summary>Заявки конкретной квартиры (абонента) — для карточки квартиры.</summary>
    private static async Task<Results<Ok<List<TicketDto>>, NotFound>> ForSubscriber(
        int subscriberId, ClaimsPrincipal user, AversDbContext db, CancellationToken ct,
        DateOnly? from = null, DateOnly? to = null)
    {
        var subscriber = await db.Subscribers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subscriberId, ct);
        if (subscriber is null)
            return TypedResults.NotFound();

        // По квартире, а не только по лицевому счёту: заявки, принятые без абонента, тоже относятся к ней.
        var tickets = await Query(db)
            .Where(t => t.SubscriberId == subscriberId || (t.HouseId == subscriber.HouseId && t.Apartment == subscriber.Apartment))
            .Apply(new TicketFilter { From = from, To = to })
            .ToListAsync(ct);
        return TypedResults.Ok(ToDtos(tickets, user));
    }

    private static async Task<Results<Ok<TicketDto>, NotFound>> Get(
        int id, ClaimsPrincipal user, AversDbContext db, CancellationToken ct)
    {
        var ticket = await Query(db).FirstOrDefaultAsync(t => t.Id == id, ct);
        return ticket is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(ticket, user));
    }

    private static async Task<Results<Ok<List<TicketMonthDto>>, ValidationProblem>> ByMonth(
        AversDbContext db, TimeProvider time, CancellationToken ct,
        int? year = null, int? houseId = null, TicketKind? kind = null)
    {
        var y = year ?? time.Today().Year;
        if (y is < 2000 or > 2100)
            return Invalid("year", "Год вне допустимого диапазона.");

        var tickets = await db.Tickets.AsNoTracking()
            .Apply(TicketFilter.ForYear(y) with { HouseId = houseId, Kind = kind })
            .ToListAsync(ct);
        return TypedResults.Ok(TicketJournal.ByMonth(tickets, y)
            .Select(m => new TicketMonthDto(y, m.Month, m.Accepted, m.Done, m.Open)).ToList());
    }

    private static async Task<Results<Ok<HouseServiceInfoDto>, NotFound>> ServiceInfo(
        int houseId, AversDbContext db, CancellationToken ct)
    {
        var h = await db.Houses.AsNoTracking().Include(x => x.Street).Include(x => x.Organization)
            .FirstOrDefaultAsync(x => x.Id == houseId, ct);
        if (h is null)
            return TypedResults.NotFound();

        var open = await db.Tickets.CountAsync(t => t.HouseId == houseId && t.Status != TicketStatus.Done, ct);
        return TypedResults.Ok(new HouseServiceInfoDto(
            h.Id, h.AddressLine, h.Zone, h.Serviced, h.Floors, h.Entrances, h.Risers, h.Apartments,
            h.Organization?.Name ?? "", h.Organization?.Branch ?? "", h.Organization?.Phones ?? "",
            h.Antennas, h.AerialCables, h.Amplifiers, h.PowerSupplies, h.TechFloorKey, h.OtherNotes,
            h.LastPpr, h.OpenFault, h.OpenFaultReason, h.OpenFaultPlannedFix, open));
    }

    private static async Task<Results<Created<TicketDto>, ValidationProblem>> Create(
        CreateTicketRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Fault))
            errors["fault"] = ["Опишите неисправность."];
        if (!TryParse<TicketKind>(request.Kind, out var kind))
            errors["kind"] = ["Неизвестный вид заявки."];
        CheckLengths(errors, request.Fault, null, request.Mechanic);

        var house = await db.Houses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == request.HouseId, ct);
        if (house is null)
            errors["houseId"] = ["Дом не найден."];
        Subscriber? subscriber = null;
        if (request.SubscriberId is { } sid)
        {
            subscriber = await db.Subscribers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct);
            if (subscriber is null || subscriber.HouseId != request.HouseId)
                errors["subscriberId"] = ["Абонент не найден в этом доме."];
        }
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var account = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.AppUserId(), ct);
        var now = time.GetLocalNow().DateTime;
        var ticket = new WorkTicket
        {
            Date = DateOnly.FromDateTime(now),
            Time = new TimeOnly(now.Hour, now.Minute),
            HouseId = request.HouseId,
            SubscriberId = subscriber?.Id,
            Section = Clean(request.Section) ?? subscriber?.Section ?? "",
            Apartment = Clean(request.Apartment) ?? subscriber?.Apartment ?? "",
            Phone = Clean(request.Phone) ?? subscriber?.Phone ?? "",
            Kind = kind ?? TicketKind.Antenna,
            Fault = request.Fault.Trim(),
            Mechanic = Clean(request.Mechanic) ?? "",
            RisersNote = Clean(request.RisersNote) ?? "",
            Status = TicketStatus.Accepted,
            AcceptedBy = account.DisplayName,
            AcceptedByUserId = account.Id
        };

        // Номер — следующий по порядку; при одновременном приёме уникальный индекс не даст двух одинаковых.
        for (var attempt = 0; ; attempt++)
        {
            ticket.Number = (await db.Tickets.MaxAsync(t => (int?)t.Number, ct) ?? 0) + 1;
            db.Tickets.Add(ticket);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                db.Entry(ticket).State = EntityState.Detached;
                ticket.Id = 0;
            }
        }

        var saved = await Query(db).FirstAsync(t => t.Id == ticket.Id, ct);
        return TypedResults.Created($"/api/tickets/{saved.Id}", ToDto(saved, user));
    }

    private static async Task<Results<Ok<TicketDto>, NotFound, ValidationProblem, ProblemHttpResult>> Update(
        int id, UpdateTicketRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return TypedResults.NotFound();
        if (!CanEdit(user, ticket))
            return TypedResults.Problem("Пользователь АДС правит только заявки, которые принял сам. Чужую заявку меняет начальник АДС.",
                statusCode: StatusCodes.Status403Forbidden, title: "Заявка не изменена");

        var errors = new Dictionary<string, string[]>();
        if (!TryParse<TicketKind>(request.Kind, out var kind))
            errors["kind"] = ["Неизвестный вид заявки."];
        if (!TryParse<TicketStatus>(request.Status, out var status))
            errors["status"] = ["Неизвестное состояние заявки."];
        if (request.Fault is not null && string.IsNullOrWhiteSpace(request.Fault))
            errors["fault"] = ["Описание неисправности не может быть пустым."];
        CheckLengths(errors, request.Fault, request.WorkDone, request.Mechanic);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        if (request.Fault is not null) ticket.Fault = request.Fault.Trim();
        if (request.Mechanic is not null) ticket.Mechanic = request.Mechanic.Trim();
        if (request.RisersNote is not null) ticket.RisersNote = request.RisersNote.Trim();
        if (kind is not null) ticket.Kind = kind.Value;
        if (status is not null)
        {
            ticket.Status = status.Value;
            ticket.DoneDate = status == TicketStatus.Done ? ticket.DoneDate ?? time.Today() : null;
        }
        if (request.WorkDone is not null)
            TicketJournal.ApplyWork(ticket, request.WorkDone, time.Today());

        await db.SaveChangesAsync(ct);
        var saved = await Query(db).FirstAsync(t => t.Id == id, ct);
        return TypedResults.Ok(ToDto(saved, user));
    }

    private static IQueryable<WorkTicket> Query(AversDbContext db) => db.Tickets.AsNoTracking()
        .Include(t => t.House).ThenInclude(h => h.Street)
        .Include(t => t.House).ThenInclude(h => h.Organization)
        .Include(t => t.Subscriber);

    private static async Task<List<TicketDto>> Load(AversDbContext db, TicketFilter filter, ClaimsPrincipal user, CancellationToken ct)
    {
        var tickets = await Query(db).Apply(filter)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Number)
            .Take(MaxResults)
            .ToListAsync(ct);
        return ToDtos(tickets, user);
    }

    private static List<TicketDto> ToDtos(IEnumerable<WorkTicket> tickets, ClaimsPrincipal user) =>
        tickets.Newest().Select(t => ToDto(t, user)).ToList();

    private static bool CanEdit(ClaimsPrincipal user, WorkTicket ticket) =>
        user.Role() is { } role && RoleCatalog.Can(role, AppModule.Disp, out var level) &&
        level == RoleCatalog.AccessLevel.Edit && RoleCatalog.CanEditTicket(role, user.AppUserId(), ticket);

    private static TicketDto ToDto(WorkTicket t, ClaimsPrincipal user) => new(
        t.Id, t.Number, t.Date, t.Time, t.HouseId, t.House.AddressLine, t.House.Zone, t.House.Organization?.Name ?? "",
        t.Section, t.Apartment, t.SubscriberId, t.Subscriber?.FullName ?? "", t.Subscriber?.Account ?? "", t.Phone,
        t.Kind.ToString(), Billing.KindTitle(t.Kind), t.Fault, t.WorkDone, t.Mechanic, t.Status.ToString(),
        t.AcceptedBy, t.DoneDate, t.RisersNote, CanEdit(user, t));

    private static bool TryParse<T>(string? value, out T? result) where T : struct, Enum
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (!Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return false;
        result = parsed;
        return true;
    }

    private static void CheckLengths(Dictionary<string, string[]> errors, string? fault, string? work, string? mechanic)
    {
        if (fault is { Length: > 500 }) errors["fault"] = ["Описание неисправности — не длиннее 500 символов."];
        if (work is { Length: > 500 }) errors["workDone"] = ["«Что произведено» — не длиннее 500 символов."];
        if (mechanic is { Length: > 120 }) errors["mechanic"] = ["Радиомеханик — не длиннее 120 символов."];
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
