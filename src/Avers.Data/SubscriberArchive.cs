using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Исходные операции JACK, без изменения сумм и дат.</summary>
public sealed record SubscriberArchive(List<JackPayment> Rows, int TotalCount, int Offset)
{
    public const int PageSize = 100;

    public static SubscriberArchive Load(AversDbContext db, int subscriberId, int? year = null, int offset = 0)
    {
        var query = db.JackPayments.AsNoTracking().Where(p => p.SubscriberId == subscriberId);
        if (year is { } y)
        {
            var from = new DateTime(y, 1, 1);
            var to = from.AddYears(1);
            query = query.Where(p => p.Period >= from && p.Period < to);
        }
        var count = query.Count();
        offset = count == 0 ? 0 : Math.Clamp(offset, 0, (count - 1) / PageSize * PageSize);
        return new(query.OrderByDescending(p => p.Period).ThenByDescending(p => p.PaidAt).ThenByDescending(p => p.Id)
            .Skip(offset).Take(PageSize).ToList(), count, offset);
    }
}
