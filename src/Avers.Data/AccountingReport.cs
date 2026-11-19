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
        var archiveFrom = from.ToDateTime(TimeOnly.MinValue);
        var archiveTo = to.ToDateTime(TimeOnly.MinValue);
        var jackReceipts = (from archived in db.JackPayments.AsNoTracking()
                            where archived.PaidAt >= archiveFrom && archived.PaidAt < archiveTo &&
                                  archived.Period >= new DateTime(1998, 1, 1)
                            join monthly in db.Payments.AsNoTracking().Where(p => p.ImportedFromJack && p.Amount != null)
                            on new { archived.SubscriberId, archived.Period.Year, archived.Period.Month }
                            equals new { monthly.SubscriberId, monthly.Year, monthly.Month }
                            select new { archived.PaidAt, archived.Amount, archived.Source })
            .AsEnumerable()
            .Select(p => new YearPayment { PaidAt = DateOnly.FromDateTime(p.PaidAt), Amount = p.Amount, Source = p.Source });
        var receipts = AccountingMonth.Receipts(payments.Where(p => !p.ImportedFromJack).Concat(jackReceipts), year, month);
        return new AccountingReport(AccountingMonth.Shipment(payments, year, month), receipts);
    }
}
