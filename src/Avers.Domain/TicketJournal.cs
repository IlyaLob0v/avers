namespace Avers.Domain;

/// <summary>
/// Отбор заявок для журнала АДС: период, дом, квартира, вид (антенна, кабель…), состояние, поиск по тексту.
/// Пустое поле — «не важно».
/// </summary>
public sealed record TicketFilter
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int? HouseId { get; init; }
    public int? SubscriberId { get; init; }
    public string? Apartment { get; init; }
    public TicketKind? Kind { get; init; }
    public TicketStatus? Status { get; init; }

    /// <summary>Только невыполненные (принятые, в работе, отложенные).</summary>
    public bool OnlyOpen { get; init; }

    /// <summary>Часть фамилии, неисправности, механика или номера заявки.</summary>
    public string? Text { get; init; }

    public static TicketFilter ForYear(int year) => new() { From = new(year, 1, 1), To = new(year, 12, 31) };

    public static TicketFilter ForMonth(int year, int month) =>
        new() { From = new(year, month, 1), To = new DateOnly(year, month, 1).AddMonths(1).AddDays(-1) };
}

/// <summary>Сколько заявок принято и выполнено за месяц года.</summary>
public sealed record TicketMonthStat(int Month, int Accepted, int Done, int Open);

public static class TicketJournal
{
    /// <summary>Применяет отбор. Работает и для запроса EF к базе, и для списка в памяти.</summary>
    public static IQueryable<WorkTicket> Apply(this IQueryable<WorkTicket> q, TicketFilter f)
    {
        if (f.From is { } from)
            q = q.Where(t => t.Date >= from);
        if (f.To is { } to)
            q = q.Where(t => t.Date <= to);
        if (f.HouseId is { } houseId)
            q = q.Where(t => t.HouseId == houseId);
        if (f.SubscriberId is { } subscriberId)
            q = q.Where(t => t.SubscriberId == subscriberId);
        if (!string.IsNullOrWhiteSpace(f.Apartment))
        {
            var apt = f.Apartment.Trim();
            q = q.Where(t => t.Apartment == apt);
        }
        if (f.Kind is { } kind)
            q = q.Where(t => t.Kind == kind);
        if (f.Status is { } status)
            q = q.Where(t => t.Status == status);
        if (f.OnlyOpen)
            q = q.Where(t => t.Status != TicketStatus.Done);
        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var text = f.Text.Trim().ToLower();
            var number = int.TryParse(text, out var n) ? n : (int?)null;
            q = q.Where(t =>
                t.Number == number ||
                t.Fault.ToLower().Contains(text) ||
                t.Mechanic.ToLower().Contains(text) ||
                (t.Subscriber != null && t.Subscriber.LastName.ToLower().StartsWith(text)));
        }
        return q;
    }

    public static IEnumerable<WorkTicket> Apply(this IEnumerable<WorkTicket> tickets, TicketFilter f) =>
        tickets.AsQueryable().Apply(f);

    /// <summary>Свежие сверху, как в журнале старого AversDisp.</summary>
    public static IOrderedEnumerable<WorkTicket> Newest(this IEnumerable<WorkTicket> tickets) =>
        tickets.OrderByDescending(t => t.Date).ThenByDescending(t => t.Time).ThenByDescending(t => t.Number);

    /// <summary>
    /// Заявки по месяцам года: принято (по дате приёма), выполнено (из принятых в этом месяце)
    /// и сколько из них ещё открыто. Всегда 12 строк.
    /// </summary>
    public static IReadOnlyList<TicketMonthStat> ByMonth(IEnumerable<WorkTicket> tickets, int year)
    {
        var inYear = tickets.Where(t => t.Date.Year == year).ToList();
        return Enumerable.Range(1, 12).Select(m =>
        {
            var month = inYear.Where(t => t.Date.Month == m).ToList();
            var done = month.Count(t => t.Status == TicketStatus.Done);
            return new TicketMonthStat(m, month.Count, done, month.Count - done);
        }).ToList();
    }

    /// <summary>Годы, за которые есть заявки, плюс текущий — для выбора года в журнале.</summary>
    public static IReadOnlyList<int> Years(IEnumerable<WorkTicket> tickets, DateOnly today) =>
        tickets.Select(t => t.Date.Year).Append(today.Year).Distinct().OrderDescending().ToList();

    /// <summary>
    /// Записать результат работы: заполненное «что произведено» закрывает заявку и ставит дату выполнения,
    /// пустое — возвращает в работу.
    /// </summary>
    public static void ApplyWork(WorkTicket ticket, string workDone, DateOnly today)
    {
        ticket.WorkDone = workDone.Trim();
        if (ticket.WorkDone.Length > 0)
        {
            if (ticket.Status != TicketStatus.Done)
                ticket.DoneDate = today;
            ticket.Status = TicketStatus.Done;
        }
        else if (ticket.Status == TicketStatus.Done)
        {
            ticket.Status = TicketStatus.InProgress;
            ticket.DoneDate = null;
        }
    }
}
