using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Сколько заявок по дому принято и выполнено за период — для отчёта и итогов дня.</summary>
public sealed record TicketHouseStat(int HouseId, int Accepted, int Done);

/// <summary>
/// Чтение заявок частями: журнал по отбору, сводки и квартира открытой заявки. Одни и те же запросы
/// выполняют сервер и учебная база; вся история заявок в память не читается.
/// </summary>
public static class TicketQueries
{
    /// <summary>Сколько строк журнала отдаётся за раз; остальное — сузив отбор.</summary>
    public const int MaxResults = 1000;

    /// <summary>
    /// Строки журнала по отбору, свежие сверху, не больше <see cref="MaxResults"/>. Дом — с улицей и организацией;
    /// у абонента только ФИО и лицевой счёт, остальное — <see cref="Apartment"/> для открытой заявки.
    /// </summary>
    public static List<WorkTicket> Page(AversDbContext db, TicketFilter filter) =>
        Rows(db.Tickets.AsNoTracking().Apply(filter)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Number)
            .Take(MaxResults));

    /// <summary>Одна заявка в том же виде, что строка журнала; null — нет такой.</summary>
    public static WorkTicket? One(AversDbContext db, int id) =>
        Rows(db.Tickets.AsNoTracking().Where(t => t.Id == id)).FirstOrDefault();

    /// <summary>
    /// Заявки квартиры абонента (правило <see cref="ApartmentTickets.For"/>): его собственные и принятые по адресу
    /// квартиры без абонента. null — абонента нет.
    /// </summary>
    public static List<WorkTicket>? ForApartment(AversDbContext db, int subscriberId, DateOnly? from = null, DateOnly? to = null)
    {
        var subscriber = db.Subscribers.AsNoTracking().FirstOrDefault(s => s.Id == subscriberId);
        if (subscriber is null)
            return null;
        var apartment = subscriber.Apartment.Trim();
        return Rows(db.Tickets.AsNoTracking()
            .Where(t => t.SubscriberId == subscriberId
                        || (apartment != "" && t.SubscriberId == null && t.HouseId == subscriber.HouseId && t.Apartment == apartment))
            .Apply(new TicketFilter { From = from, To = to })
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Number)
            .Take(MaxResults));
    }

    /// <summary>
    /// Сводка за год по месяцам (REQ-ADS-14) с отбором по дому, квартире и виду; даты, состояние и текст отбора
    /// не учитываются. База считает по дням, месяцы складываются здесь — по сети идут 12 строк.
    /// </summary>
    public static IReadOnlyList<TicketMonthStat> ByMonth(AversDbContext db, int year, TicketFilter filter)
    {
        var days = db.Tickets.AsNoTracking()
            .Apply(TicketFilter.ForYear(year) with { HouseId = filter.HouseId, Apartment = filter.Apartment, Kind = filter.Kind })
            .GroupBy(t => t.Date)
            .Select(g => new { Date = g.Key, Accepted = g.Count(), Done = g.Count(t => t.Status == TicketStatus.Done) })
            .ToList();
        return Enumerable.Range(1, 12).Select(m =>
        {
            var month = days.Where(d => d.Date.Month == m).ToList();
            var (accepted, done) = (month.Sum(d => d.Accepted), month.Sum(d => d.Done));
            return new TicketMonthStat(m, accepted, done, accepted - done);
        }).ToList();
    }

    /// <summary>Годы для выбора в журнале: от первой заявки до текущего, новые сверху.</summary>
    public static IReadOnlyList<int> Years(AversDbContext db, DateOnly today)
    {
        var first = db.Tickets.Min(t => (DateOnly?)t.Date)?.Year ?? today.Year;
        var last = Math.Max(today.Year, db.Tickets.Max(t => (DateOnly?)t.Date)?.Year ?? today.Year);
        return Enumerable.Range(first, last - first + 1).Reverse().ToList();
    }

    /// <summary>Принято и выполнено по домам за период (по дате приёма).</summary>
    public static List<TicketHouseStat> ByHouse(AversDbContext db, DateOnly from, DateOnly to) =>
        db.Tickets.AsNoTracking()
            .Where(t => t.Date >= from && t.Date <= to)
            .GroupBy(t => t.HouseId)
            .Select(g => new TicketHouseStat(g.Key, g.Count(), g.Count(t => t.Status == TicketStatus.Done)))
            .ToList();

    /// <summary>
    /// Квартира заявки (REQ-ADS-15): ФИО, лицевой счёт, договор, последняя оплата, примечание — без оплат и прочих полей.
    /// null — заявки нет или она принята без абонента.
    /// </summary>
    public static Subscriber? Apartment(AversDbContext db, int ticketId) =>
        db.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId && t.Subscriber != null)
            .Select(t => new Subscriber
            {
                Id = t.Subscriber!.Id,
                HouseId = t.Subscriber.HouseId,
                LastName = t.Subscriber.LastName,
                FirstName = t.Subscriber.FirstName,
                Patronymic = t.Subscriber.Patronymic,
                Account = t.Subscriber.Account,
                Section = t.Subscriber.Section,
                Apartment = t.Subscriber.Apartment,
                ContractNumber = t.Subscriber.ContractNumber,
                ContractDate = t.Subscriber.ContractDate,
                ContractTerminated = t.Subscriber.ContractTerminated,
                ContractHolder = t.Subscriber.ContractHolder,
                LastPaymentDate = t.Subscriber.LastPaymentDate,
                Notes = t.Subscriber.Notes,
                PzhreuIndex = t.Subscriber.PzhreuIndex
            })
            .FirstOrDefault();

    /// <summary>Заявки с домом (улица, организация) и кратким абонентом: ФИО и лицевой счёт.</summary>
    private static List<WorkTicket> Rows(IQueryable<WorkTicket> query)
    {
        var rows = query.Select(t => new
        {
            Ticket = t,
            t.House,
            t.House.Street,
            t.House.Organization,
            Subscriber = t.Subscriber == null ? null : new Subscriber
            {
                Id = t.Subscriber.Id,
                HouseId = t.Subscriber.HouseId,
                LastName = t.Subscriber.LastName,
                FirstName = t.Subscriber.FirstName,
                Patronymic = t.Subscriber.Patronymic,
                Account = t.Subscriber.Account
            }
        }).ToList();
        foreach (var r in rows)
        {
            r.House.Street = r.Street;
            r.House.Organization = r.Organization;
            r.Ticket.House = r.House;
            r.Ticket.Subscriber = r.Subscriber;
        }
        return rows.Select(r => r.Ticket).Newest().ToList();
    }
}
