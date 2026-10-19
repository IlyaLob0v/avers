using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

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
    string? Case);

/// <summary>
/// Долги по всей базе: суммы оплат по абонентам считает СУБД, в память приходит по строке на абонента.
/// Правила те же, что в <see cref="Billing"/>: месяцы по текущий включительно.
/// </summary>
public sealed class Debts
{
    public List<Debtor> Debtors { get; set; } = [];

    /// <summary>Долг всех абонентов дома, для отчёта по заявкам.</summary>
    public Dictionary<int, decimal> ByHouse { get; set; } = [];

    public static Debts Load(AversDbContext db, DateOnly today)
    {
        var dueMonth = today.Year * 12 + today.Month;
        var sums = db.Payments
            .Where(p => p.Year * 12 + p.Month <= dueMonth)
            .GroupBy(p => p.SubscriberId)
            .Select(g => new
            {
                SubscriberId = g.Key,
                Charged = g.Sum(p => p.Tariff),
                Paid = g.Sum(p => p.Amount ?? 0),
                Unpaid = g.Count(p => p.Amount == null)
            })
            .ToDictionary(x => x.SubscriberId);
        var cases = db.CourtCases.AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new { c.SubscriberId, Text = c.DelaAversId + " · " + c.OrderNumber })
            .AsEnumerable()
            .DistinctBy(c => c.SubscriberId)
            .ToDictionary(c => c.SubscriberId, c => c.Text);
        var rows = db.Subscribers.AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.HouseId, s.Account, s.LastName, s.FirstName, s.Patronymic, s.Apartment, s.LastPaymentDate })
            .ToList();

        var debts = new Debts();
        foreach (var r in rows)
        {
            var sum = sums.GetValueOrDefault(r.Id);
            var debt = sum is null ? 0 : Math.Max(0, sum.Charged - sum.Paid);
            debts.ByHouse[r.HouseId] = debts.ByHouse.GetValueOrDefault(r.HouseId) + debt;
            var old = Billing.IsOldPayer(r.LastPaymentDate, today);
            if (debt >= Billing.DebtHighlight || old)
            {
                var name = new Subscriber { LastName = r.LastName, FirstName = r.FirstName, Patronymic = r.Patronymic }.FullName;
                debts.Debtors.Add(new Debtor(r.Id, r.HouseId, r.Account, name, r.Apartment, debt, sum?.Unpaid ?? 0, r.LastPaymentDate, old, cases.GetValueOrDefault(r.Id)));
            }
        }
        return debts;
    }
}
