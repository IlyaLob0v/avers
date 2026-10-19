using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Абоненты одного дома со всеми оплатами, судебными делами и едиными квитанциями. Настольная программа
/// грузит их при выборе дома, а не всю базу сразу. По сети уходят плоско, связываются в <see cref="Link"/>.
/// </summary>
public sealed class HouseSubscribers
{
    public List<Subscriber> Subscribers { get; set; } = [];
    public List<YearPayment> Payments { get; set; } = [];
    public List<CourtCase> CourtCases { get; set; } = [];
    public List<UnifiedReceipt> Receipts { get; set; } = [];

    public static HouseSubscribers Load(AversDbContext db, int houseId) => new()
    {
        Subscribers = db.Subscribers.AsNoTracking().Where(s => s.HouseId == houseId).ToList(),
        Payments = db.Payments.AsNoTracking().Where(p => p.Subscriber.HouseId == houseId).ToList(),
        CourtCases = db.CourtCases.AsNoTracking().Where(c => c.Subscriber.HouseId == houseId).ToList(),
        Receipts = db.UnifiedReceipts.AsNoTracking().Where(r => r.Subscriber.HouseId == houseId).ToList()
    };

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
