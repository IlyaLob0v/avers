using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Список квартир дома и история только выбранного абонента.
/// По сети уходят плоско, связываются в <see cref="Link"/>.
/// </summary>
public sealed class HouseSubscribers
{
    public List<Subscriber> Subscribers { get; set; } = [];
    public List<YearPayment> Payments { get; set; } = [];
    public List<CourtCase> CourtCases { get; set; } = [];
    public List<UnifiedReceipt> Receipts { get; set; } = [];

    public static HouseSubscribers Load(AversDbContext db, int houseId, int? subscriberId = null)
    {
        var result = new HouseSubscribers
        {
            Subscribers = db.Subscribers.AsNoTracking().Where(s => s.HouseId == houseId).OrderBy(s => s.Id).ToList()
        };
        var selected = subscriberId is { } id
            ? result.Subscribers.FirstOrDefault(s => s.Id == id)
            : result.Subscribers.FirstOrDefault(s => s.Account == DemoSeeder.StartAccount) ?? result.Subscribers.FirstOrDefault();
        if (selected is null) return result;
        result.Payments = db.Payments.AsNoTracking().Where(p => p.SubscriberId == selected.Id).ToList();
        result.CourtCases = db.CourtCases.AsNoTracking().Where(c => c.SubscriberId == selected.Id).ToList();
        result.Receipts = db.UnifiedReceipts.AsNoTracking().Where(r => r.SubscriberId == selected.Id).ToList();
        return result;
    }

    /// <summary>Расставляет связи; дом берётся из уже загруженного снимка.</summary>
    public List<Subscriber> Link(House house)
    {
        var payments = Payments.ToLookup(x => x.SubscriberId);
        var cases = CourtCases.ToLookup(x => x.SubscriberId);
        var receipts = Receipts.ToLookup(x => x.SubscriberId);
        foreach (var s in Subscribers)
        {
            s.House = house;
            s.Payments = payments[s.Id].OrderBy(x => x.Year).ThenBy(x => x.Month).ToList();
            s.CourtCases = cases[s.Id].ToList();
            s.Receipts = receipts[s.Id].ToList();
        }
        return Subscribers;
    }
}
