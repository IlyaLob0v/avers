using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Итоги месяца для 1С (<see cref="AccountingMonth"/>): из базы читаются только оплаты этого месяца.</summary>
public sealed record AccountingReport(List<AccountingMonth.ShipmentLine> Shipment, List<AccountingMonth.ReceiptLine> Receipts)
{
    public static AccountingReport Load(AversDbContext db, int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var payments = db.Payments.AsNoTracking()
            .Where(p => (p.Year == year && p.Month == month) || (p.PaidAt >= from && p.PaidAt < to))
            .ToList();
        return new AccountingReport(AccountingMonth.Shipment(payments, year, month), AccountingMonth.Receipts(payments, year, month));
    }
}
