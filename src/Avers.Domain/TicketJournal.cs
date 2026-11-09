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

    /// <summary>Только просроченные на этот день (<see cref="TicketJournal.IsOverdue"/>).</summary>
    public DateOnly? OverdueOn { get; init; }

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
    /// <summary>Срок выполнения заявки, дней от приёма (Q-31).</summary>
    public const int DueDays = 3;

    public static DateOnly DueDate(WorkTicket ticket) => ticket.Date.AddDays(DueDays);

    /// <summary>Срок прошёл, а заявка принята или в работе. Отложенная не просрочена: её отложили с причиной.</summary>
    public static bool IsOverdue(WorkTicket ticket, DateOnly today) =>
        ticket.Status is TicketStatus.Accepted or TicketStatus.InProgress && DueDate(ticket) < today;

    /// <summary>
    /// Список для ОЭК: заявки, принятые за день. Если выполнены не все — null, список не выводится (REQ-ADS-09, Q-02).
    /// </summary>
    public static IReadOnlyList<WorkTicket>? ForOek(IEnumerable<WorkTicket> tickets, DateOnly day)
    {
        var list = tickets.Where(t => t.Date == day).OrderBy(t => t.Time).ThenBy(t => t.Number).ToList();
        return list.All(t => t.Status == TicketStatus.Done) ? list : null;
    }

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
        if (f.OverdueOn is { } day)
        {
            var acceptedBefore = day.AddDays(-DueDays);
            q = q.Where(t => (t.Status == TicketStatus.Accepted || t.Status == TicketStatus.InProgress) && t.Date < acceptedBefore);
        }
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

    /// <summary>
    /// Записать результат с явным состоянием (например, «отложена» с пометкой, почему). Выполненная получает дату работы:
    /// указанную, иначе прежнюю, иначе сегодняшнюю; у невыполненной даты нет.
    /// </summary>
    public static void SetResult(WorkTicket ticket, string workDone, TicketStatus status, DateOnly? doneDate, DateOnly today)
    {
        ticket.WorkDone = workDone.Trim();
        ticket.Status = status;
        ticket.DoneDate = status == TicketStatus.Done ? doneDate ?? ticket.DoneDate ?? today : null;
    }
}
