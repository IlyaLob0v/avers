using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Всё, что настольная программа держит в памяти после входа. Таблицы читаются плоско, по сети
/// уходят без связей, а связываются по ключам в <see cref="Link"/> уже на месте.
/// Абонентов со счетами здесь нет: их грузят по дому (<see cref="HouseSubscribers"/>).
/// Заявки получает только тот, кому они положены по роли, вместе с абонентами этих заявок.
/// </summary>
public sealed class AversSnapshot
{
    public int PaymentWarningMonths { get; set; } = PaymentAge.DefaultWarningMonths;
    public List<Organization> Organizations { get; set; } = [];
    public List<Street> Streets { get; set; } = [];
    public List<House> Houses { get; set; } = [];
    public List<HouseScheme> Schemes { get; set; } = [];
    public List<PprEntry> Ppr { get; set; } = [];
    public List<IntercomPanel> Intercoms { get; set; } = [];
    public List<IntercomTicket> IntercomTickets { get; set; } = [];
    public List<Subscriber> Subscribers { get; set; } = [];
    public List<WorkTicket> Tickets { get; set; } = [];
    public List<Mechanic> Mechanics { get; set; } = [];
    public List<Material> Materials { get; set; } = [];

    public static AversSnapshot Load(AversDbContext db, UserRole role)
    {
        var snapshot = new AversSnapshot
        {
            PaymentWarningMonths = Settings.PaymentWarningMonths(db),
            Organizations = db.Organizations.AsNoTracking().ToList(),
            Streets = db.Streets.AsNoTracking().OrderBy(x => x.Name).ToList(),
            Houses = db.Houses.AsNoTracking().ToList(),
            Schemes = db.HouseSchemes.AsNoTracking().ToList(),
            Ppr = db.PprEntries.AsNoTracking().ToList(),
            Intercoms = db.IntercomPanels.AsNoTracking().ToList(),
            IntercomTickets = db.IntercomTickets.AsNoTracking().OrderByDescending(x => x.Date).ToList(),
            Mechanics = db.Mechanics.AsNoTracking().OrderBy(x => x.FullName).ToList(),
            Materials = db.Materials.AsNoTracking().ToList()
        };
        if (RoleCatalog.Can(role, AppModule.Disp, out _))
        {
            snapshot.Tickets = db.Tickets.AsNoTracking().ToList();
            if (RoleCatalog.Can(role, AppModule.Abon, out _))
                snapshot.Subscribers = db.Subscribers.AsNoTracking().Where(s => s.Tickets.Any()).ToList();
        }
        return snapshot;
    }

    /// <summary>Расставляет навигационные свойства по ключам.</summary>
    public AversSnapshot Link()
    {
        var streets = Streets.ToDictionary(x => x.Id);
        var organizations = Organizations.ToDictionary(x => x.Id);
        var houses = Houses.ToDictionary(x => x.Id);
        var subscribers = Subscribers.ToDictionary(x => x.Id);

        var schemes = Schemes.ToLookup(x => x.HouseId);
        var ppr = Ppr.ToLookup(x => x.HouseId);
        var intercoms = Intercoms.ToLookup(x => x.HouseId);
        foreach (var h in Houses)
        {
            h.Street = streets[h.StreetId];
            h.Organization = h.OrganizationId is { } org ? organizations.GetValueOrDefault(org) : null;
            h.Schemes = schemes[h.Id].ToList();
            h.Ppr = ppr[h.Id].OrderBy(x => x.Year).ThenBy(x => x.Month).ToList();
            h.Intercoms = intercoms[h.Id].OrderBy(x => x.Entrance).ToList();
        }
        foreach (var s in Subscribers)
            s.House = houses[s.HouseId];

        foreach (var t in Tickets)
        {
            t.House = houses[t.HouseId];
            t.Subscriber = t.SubscriberId is { } id ? subscribers.GetValueOrDefault(id) : null;
        }
        return this;
    }
}
