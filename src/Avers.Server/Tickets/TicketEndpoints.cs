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
    public const int MaxResults = TicketQueries.MaxResults;

    public static void MapTickets(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Заявки").RequireAuthorization(Policies.TicketsView);

        api.MapGet("/tickets", List);
        api.MapGet("/tickets/by-month", ByMonth);
        api.MapGet("/tickets/by-house", ByHouse);
        api.MapGet("/tickets/years", (AversDbContext db, TimeProvider time) => TypedResults.Ok(TicketQueries.Years(db, time.Today())));
        api.MapGet("/tickets/{id:int}", Get);
        api.MapGet("/tickets/{id:int}/apartment", Apartment).RequireAuthorization(Policies.SubscribersView).LogsView("Квартира заявки", AccessLogFilter.TicketSubscriber("id"));
        api.MapPost("/tickets", Create).RequireAuthorization(Policies.TicketsCreate);
        api.MapPut("/tickets/{id:int}", Update).RequireAuthorization(Policies.TicketsCreate);
        api.MapGet("/houses/{houseId:int}/tickets", ForHouse);
        api.MapGet("/houses/{houseId:int}/service-info", ServiceInfo);
        api.MapGet("/subscribers/{subscriberId:int}/tickets", ForSubscriber).LogsView("Заявки абонента", AccessLogFilter.Subscriber("subscriberId"));
    }

    /// <summary>
    /// Журнал: <c>from</c>/<c>to</c> или <c>year</c> (+ <c>month</c>), <c>houseId</c>, <c>apartment</c>,
    /// <c>subscriberId</c>, <c>kind</c> (Antenna, Cable…), <c>status</c>, <c>open=true</c>, <c>q</c> — текст.
    /// </summary>
    private static Results<Ok<List<TicketDto>>, ValidationProblem> List(
        ClaimsPrincipal user, AversDbContext db,
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
        return TypedResults.Ok(ToDtos(TicketQueries.Page(db, filter), user));
    }

    private static Ok<List<TicketDto>> ForHouse(
        int houseId, ClaimsPrincipal user, AversDbContext db,
        DateOnly? from = null, DateOnly? to = null, TicketKind? kind = null) =>
        TypedResults.Ok(ToDtos(TicketQueries.Page(db, new TicketFilter { HouseId = houseId, From = from, To = to, Kind = kind }), user));

    /// <summary>Заявки конкретной квартиры (абонента) — для карточки квартиры.</summary>
    private static Results<Ok<List<TicketDto>>, NotFound> ForSubscriber(
        int subscriberId, ClaimsPrincipal user, AversDbContext db, DateOnly? from = null, DateOnly? to = null) =>
        TicketQueries.ForApartment(db, subscriberId, from, to) is { } tickets
            ? TypedResults.Ok(ToDtos(tickets, user))
            : TypedResults.NotFound();

    private static Results<Ok<TicketDto>, NotFound> Get(int id, ClaimsPrincipal user, AversDbContext db) =>
        TicketQueries.One(db, id) is { } ticket ? TypedResults.Ok(ToDto(ticket, user)) : TypedResults.NotFound();

    /// <summary>Квартира заявки (REQ-ADS-15) — только для открытой заявки; принятая без абонента — 404.</summary>
    private static Results<Ok<TicketApartmentDto>, NotFound> Apartment(int id, AversDbContext db) =>
        TicketQueries.Apartment(db, id) is { } s
            ? TypedResults.Ok(new TicketApartmentDto(s.Id, s.HouseId, s.LastName, s.FirstName, s.Patronymic, s.Account,
                s.Section, s.Apartment, s.ContractNumber, s.ContractDate, s.ContractTerminated, s.ContractHolder,
                s.LastPaymentDate, s.Notes, s.PzhreuIndex))
            : TypedResults.NotFound();

    /// <summary>Сводка за год по месяцам (REQ-ADS-14): база считает, по сети идут 12 строк.</summary>
    private static Results<Ok<List<TicketMonthDto>>, ValidationProblem> ByMonth(
        AversDbContext db, TimeProvider time,
        int? year = null, int? houseId = null, string? apartment = null, TicketKind? kind = null)
    {
        var y = year ?? time.Today().Year;
        if (y is < 2000 or > 2100)
            return Invalid("year", "Год вне допустимого диапазона.");

        return TypedResults.Ok(TicketQueries.ByMonth(db, y, new TicketFilter { HouseId = houseId, Apartment = apartment, Kind = kind })
            .Select(m => new TicketMonthDto(y, m.Month, m.Accepted, m.Done, m.Open)).ToList());
    }

    /// <summary>Принято и выполнено по домам за период — итоговый отчёт и итоги дня для ОЭК.</summary>
    private static Results<Ok<List<TicketHouseCountDto>>, ValidationProblem> ByHouse(AversDbContext db, DateOnly from, DateOnly to) =>
        from > to
            ? Invalid("from", "Начало периода позже конца.")
            : TypedResults.Ok(TicketQueries.ByHouse(db, from, to).Select(s => new TicketHouseCountDto(s.HouseId, s.Accepted, s.Done)).ToList());

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
        var ticket = Avers.Data.Tickets.Accept(db, new WorkTicket
        {
            Date = DateOnly.FromDateTime(now),
            Time = new TimeOnly(now.Hour, now.Minute),
            HouseId = request.HouseId,
            SubscriberId = subscriber?.Id,
            Section = request.Section ?? "",
            Apartment = request.Apartment ?? "",
            Phone = request.Phone ?? "",
            Kind = kind ?? TicketKind.Antenna,
            Fault = request.Fault,
            Mechanic = request.Mechanic ?? "",
            RisersNote = request.RisersNote ?? "",
            AcceptedBy = account.DisplayName,
            AcceptedByUserId = account.Id
        });

        var saved = TicketQueries.One(db, ticket.Id)!;
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
        if (request.DoneDate is { } doneDate && (doneDate > time.Today() || doneDate < ticket.Date))
            errors["doneDate"] = ["Дата выполнения — не раньше приёма заявки и не позже сегодняшнего дня."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var before = ChangeLog.TicketState(ticket);
        if (request.Fault is not null) ticket.Fault = request.Fault.Trim();
        if (request.Mechanic is not null) ticket.Mechanic = request.Mechanic.Trim();
        if (request.RisersNote is not null) ticket.RisersNote = request.RisersNote.Trim();
        if (kind is not null) ticket.Kind = kind.Value;
        if (status is not null)
            TicketJournal.SetResult(ticket, request.WorkDone ?? ticket.WorkDone, status.Value, request.DoneDate, time.Today());
        else if (request.WorkDone is not null)
            TicketJournal.ApplyWork(ticket, request.WorkDone, time.Today());
        ChangeLog.TicketEdit(db, time.GetUtcNow(), ChangeLog.Who(db, user.AppUserId()), ticket, before);

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(TicketQueries.One(db, id)!, user));
    }

    private static List<TicketDto> ToDtos(IEnumerable<WorkTicket> tickets, ClaimsPrincipal user) =>
        tickets.Select(t => ToDto(t, user)).ToList();

    private static bool CanEdit(ClaimsPrincipal user, WorkTicket ticket) =>
        user.Role() is { } role && RoleCatalog.Can(role, AppModule.Disp, out var level) &&
        level == RoleCatalog.AccessLevel.Edit && RoleCatalog.CanEditTicket(role, user.AppUserId(), ticket);

    private static TicketDto ToDto(WorkTicket t, ClaimsPrincipal user) => new(
        t.Id, t.Number, t.Date, t.Time, t.HouseId, t.House.AddressLine, t.House.Zone, t.House.Organization?.Name ?? "",
        t.Section, t.Apartment, t.SubscriberId, t.Subscriber?.FullName ?? "", t.Subscriber?.Account ?? "", t.Phone,
        t.Kind.ToString(), Billing.KindTitle(t.Kind), t.Fault, t.WorkDone, t.Mechanic, t.Status.ToString(),
        t.AcceptedBy, t.DoneDate, t.RisersNote, CanEdit(user, t), t.AcceptedByUserId,
        t.Subscriber?.LastName ?? "", t.Subscriber?.FirstName ?? "", t.Subscriber?.Patronymic ?? "");

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

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
