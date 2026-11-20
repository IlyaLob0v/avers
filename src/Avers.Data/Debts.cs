using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

public sealed record DebtorFilter(decimal DebtFrom = 0, int UnpaidFrom = 0, string Address = "", bool? HasCase = null, bool? IsLegalEntity = null);

/// <summary>Должник для юристов: долг от <see cref="Billing.DebtHighlight"/> ₽ или давно не платил.</summary>
public sealed record Debtor(
    int SubscriberId,
    int HouseId,
    string Account,
    string FullName,
    string Apartment,
    decimal Debt,
    int Unpaid,
    DateOnly? LastPaymentDate,
    bool IsOld,
    CourtCase? Case,
    DateOnly? OldestUnpaid,
    DateOnly? ContractTerminated,
    bool IsLegalEntity)
{
    /// <summary>До какого дня можно взыскать самый старый неоплаченный месяц.</summary>
    public DateOnly? LimitationEnds => OldestUnpaid is { } m ? LegalDeadline.LimitationEnds(m.Year, m.Month) : null;
}

/// <summary>
/// Отбор и страницы должников считает СУБД; в память приходит только выбранная страница.
/// Правила те же, что в <see cref="Billing.Due"/>: месяцы до текущего.
/// </summary>
public sealed class Debts
{
    public const int PageSize = 100;
    public List<Debtor> Debtors { get; set; } = [];
    public int TotalCount { get; set; }
    public int Offset { get; set; }

    public static Debts Load(AversDbContext db, DateOnly today, DebtorFilter? filter = null, int offset = 0, bool all = false)
    {
        var query = Filtered(db, today, filter);
        var total = query.Count();
        offset = all || total == 0 ? 0 : Math.Clamp(offset, 0, (total - 1) / PageSize * PageSize);
        var ordered = query.OrderBy(r => r.Subscriber.Id);
        var selected = all ? ordered : ordered.Skip(offset).Take(PageSize);
        var rows = selected.ToList();
        var oldest = OldestUnpaid(db, rows, today).ToDictionary(p => p.SubscriberId, p => p.Period);
        return Result(rows, LatestCases(db, rows).ToList(), oldest, today, total, offset);
    }

    public static async Task<Debts> LoadAsync(AversDbContext db, DateOnly today, DebtorFilter? filter = null,
        int offset = 0, bool all = false, CancellationToken cancellationToken = default)
    {
        var query = Filtered(db, today, filter);
        var total = await query.CountAsync(cancellationToken);
        offset = all || total == 0 ? 0 : Math.Clamp(offset, 0, (total - 1) / PageSize * PageSize);
        var ordered = query.OrderBy(r => r.Subscriber.Id);
        var selected = all ? ordered : ordered.Skip(offset).Take(PageSize);
        var rows = await selected.ToListAsync(cancellationToken);
        var cases = await LatestCases(db, rows).ToListAsync(cancellationToken);
        var oldest = await OldestUnpaid(db, rows, today).ToDictionaryAsync(p => p.SubscriberId, p => p.Period, cancellationToken);
        return Result(rows, cases, oldest, today, total, offset);
    }

    private static IQueryable<Balance> Filtered(AversDbContext db, DateOnly today, DebtorFilter? filter)
    {
        filter ??= new();
        var oldBefore = today.AddYears(-Billing.OldPaymentYears);
        return Balances(db, today, filter)
            .Where(r => r.Debt >= Billing.DebtHighlight || r.Subscriber.LastPaymentDate == null || r.Subscriber.LastPaymentDate <= oldBefore)
            .Where(r => r.Debt >= filter.DebtFrom && r.Unpaid >= filter.UnpaidFrom);
    }

    private static IQueryable<CourtCase> LatestCases(AversDbContext db, List<Balance> rows)
    {
        var ids = rows.Select(r => r.Subscriber.Id).ToArray();
        return db.CourtCases.AsNoTracking().Where(c => ids.Contains(c.SubscriberId)
                && c.Id == db.CourtCases.Where(x => x.SubscriberId == c.SubscriberId).Max(x => x.Id));
    }

    private sealed record UnpaidPeriod(int SubscriberId, int Period);

    private static IQueryable<UnpaidPeriod> OldestUnpaid(AversDbContext db, List<Balance> rows, DateOnly today)
    {
        var ids = rows.Select(r => r.Subscriber.Id).ToArray();
        var dueMonth = Billing.LastDueMonth(today);
        return db.Payments.AsNoTracking().Where(p => ids.Contains(p.SubscriberId) && p.Amount == null && p.Year * 12 + p.Month <= dueMonth)
            .GroupBy(p => p.SubscriberId).Select(g => new UnpaidPeriod(g.Key, g.Min(p => p.Year * 12 + p.Month - 1)));
    }

    private static Debts Result(List<Balance> rows, List<CourtCase> latestCases, Dictionary<int, int> oldest,
        DateOnly today, int total, int offset)
    {
        var cases = latestCases.ToDictionary(c => c.SubscriberId);
        var debts = new Debts { TotalCount = total, Offset = offset };
        foreach (var r in rows)
        {
            var s = r.Subscriber;
            debts.Debtors.Add(new Debtor(s.Id, s.HouseId, s.Account, s.FullName, s.Apartment, r.Debt, r.Unpaid,
                s.LastPaymentDate, Billing.IsOldPayer(s.LastPaymentDate, today), cases.GetValueOrDefault(s.Id),
                oldest.TryGetValue(s.Id, out var m) ? new DateOnly(m / 12, m % 12 + 1, 1) : null, s.ContractTerminated, s.IsLegalEntity));
        }
        return debts;
    }

    public static Dictionary<int, decimal> LoadHouseTotals(AversDbContext db, DateOnly today) =>
        Balances(db, today).GroupBy(r => r.Subscriber.HouseId)
            .Select(g => new { HouseId = g.Key, Debt = g.Sum(r => r.Debt) })
            .ToDictionary(r => r.HouseId, r => r.Debt);

    private sealed class Balance
    {
        public Subscriber Subscriber { get; set; } = null!;
        public decimal Debt { get; set; }
        public int Unpaid { get; set; }
    }

    private static IQueryable<Balance> Balances(AversDbContext db, DateOnly today, DebtorFilter? filter = null)
    {
        var dueMonth = Billing.LastDueMonth(today);
        var subscribers = db.Subscribers.AsNoTracking();
        if (filter?.IsLegalEntity is { } legal)
            subscribers = subscribers.Where(s => s.IsLegalEntity == legal);
        if (filter?.HasCase is { } hasCase)
            subscribers = subscribers.Where(s => db.CourtCases.Any(c => c.SubscriberId == s.Id) == hasCase);
        foreach (var word in (filter?.Address ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var text = word.ToLowerInvariant();
            subscribers = subscribers.Where(s => ("ул. " + s.House.Street.Name + ", д. " + s.House.Number + ", кв. " + s.Apartment)
                .ToLower().Contains(text));
        }
        var payments = db.Payments.AsNoTracking().Where(p => p.Year * 12 + p.Month <= dueMonth);
        if (filter?.IsLegalEntity is not null || filter?.HasCase is not null || !string.IsNullOrWhiteSpace(filter?.Address))
        {
            var selectedIds = subscribers.Select(s => s.Id);
            payments = payments.Where(p => selectedIds.Contains(p.SubscriberId));
        }
        var sums = payments
            .GroupBy(p => p.SubscriberId)
            .Select(g => new
            {
                SubscriberId = g.Key,
                Debt = g.Sum(p => p.Tariff) - g.Sum(p => p.Amount ?? 0),
                Unpaid = g.Count(p => p.Amount == null)
            });
        if (db.Database.IsNpgsql())
        {
            var history = db.PaymentYearTotals.AsNoTracking().Where(p => p.Year < today.Year);
            if (filter?.IsLegalEntity is not null || filter?.HasCase is not null || !string.IsNullOrWhiteSpace(filter?.Address))
            {
                var selectedIds = subscribers.Select(s => s.Id);
                history = history.Where(p => selectedIds.Contains(p.SubscriberId));
            }
            var yearly = history.Select(p => new { p.SubscriberId, Debt = p.Charged - p.Paid, p.Unpaid });
            var current = payments.Where(p => p.Year == today.Year && p.Month < today.Month)
                .GroupBy(p => p.SubscriberId)
                .Select(g => new { SubscriberId = g.Key, Debt = g.Sum(p => p.Tariff) - g.Sum(p => p.Amount ?? 0), Unpaid = g.Count(p => p.Amount == null) });
            sums = yearly.Concat(current).GroupBy(p => p.SubscriberId)
                .Select(g => new { SubscriberId = g.Key, Debt = g.Sum(p => p.Debt), Unpaid = g.Sum(p => p.Unpaid) });
        }
        return from s in subscribers
               join sum in sums on s.Id equals sum.SubscriberId into joined
               from sum in joined.DefaultIfEmpty()
               select new Balance
               {
                   Subscriber = s,
                   Debt = ((decimal?)sum.Debt ?? 0) > 0 ? (decimal?)sum.Debt ?? 0 : 0,
                   Unpaid = (int?)sum.Unpaid ?? 0
               };
    }
}
