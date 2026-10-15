namespace Avers.Domain;

/// <summary>Заявки по конкретной квартире — для карточки квартиры.</summary>
public static class ApartmentTickets
{
    /// <summary>
    /// Заявки, привязанные к абоненту, и заявки, принятые по адресу его квартиры без привязки
    /// (диспетчер мог не найти абонента при приёме). Новые сверху.
    /// </summary>
    public static IEnumerable<WorkTicket> For(IEnumerable<WorkTicket> tickets, Subscriber subscriber, int? year = null) =>
        tickets.Where(t => t.SubscriberId == subscriber.Id
                           || (t.SubscriberId is null && t.HouseId == subscriber.HouseId
                               && !string.IsNullOrWhiteSpace(t.Apartment)
                               && string.Equals(t.Apartment.Trim(), subscriber.Apartment.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(t => year is null || t.Date.Year == year)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Time);
}
