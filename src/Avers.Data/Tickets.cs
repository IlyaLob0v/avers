using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Приём заявки АДС: одно правило для сервера и учебной базы.</summary>
public static class Tickets
{
    /// <summary>
    /// Сохраняет заявку со следующим номером. Пустые подъезд, квартира и телефон берутся из карточки абонента.
    /// </summary>
    public static WorkTicket Accept(AversDbContext db, WorkTicket draft)
    {
        var subscriber = draft.SubscriberId is { } id ? db.Subscribers.AsNoTracking().FirstOrDefault(s => s.Id == id) : null;
        var ticket = new WorkTicket
        {
            Date = draft.Date,
            Time = draft.Time,
            HouseId = draft.HouseId,
            SubscriberId = draft.SubscriberId,
            Section = Clean(draft.Section) ?? subscriber?.Section ?? "",
            Apartment = Clean(draft.Apartment) ?? subscriber?.Apartment ?? "",
            Phone = Clean(draft.Phone) ?? subscriber?.Phone ?? "",
            Kind = draft.Kind,
            Fault = draft.Fault.Trim(),
            Mechanic = draft.Mechanic.Trim(),
            RisersNote = draft.RisersNote.Trim(),
            Status = TicketStatus.Accepted,
            AcceptedBy = draft.AcceptedBy,
            AcceptedByUserId = draft.AcceptedByUserId
        };

        // Номер — следующий по порядку; при одновременном приёме уникальный индекс не даст двух одинаковых.
        for (var attempt = 0; ; attempt++)
        {
            ticket.Number = (db.Tickets.Max(t => (int?)t.Number) ?? 0) + 1;
            db.Tickets.Add(ticket);
            try
            {
                db.SaveChanges();
                return ticket;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                db.Entry(ticket).State = EntityState.Detached;
                ticket.Id = 0;
            }
        }
    }

    private static string? Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
