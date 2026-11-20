using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Итоги месяца для 1С: группировка в СУБД, в память приходят только строки отчёта.</summary>
public sealed record AccountingReport(List<AccountingMonth.ShipmentLine> Shipment, List<AccountingMonth.ReceiptLine> Receipts)
{
    public static AccountingReport Load(AversDbContext db, int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var shipment = db.Payments.AsNoTracking()
            .Where(p => p.Year == year && p.Month == month && p.Tariff > 0)
            .GroupBy(p => p.Tariff).OrderBy(g => g.Key)
            .Select(g => new AccountingMonth.ShipmentLine(g.Key, g.Count(), g.Sum(p => p.Tariff))).ToList();
        var payments = db.Payments.AsNoTracking()
            .Where(p => !p.ImportedFromJack && p.Amount != null && p.PaidAt >= from && p.PaidAt < to)
            .Select(p => new { p.Source, Amount = p.Amount ?? 0 });
        var archiveFrom = from.ToDateTime(TimeOnly.MinValue);
        var archiveTo = to.ToDateTime(TimeOnly.MinValue);
        var jackReceipts = from archived in db.JackPayments.AsNoTracking()
                           where archived.PaidAt >= archiveFrom && archived.PaidAt < archiveTo &&
                                 archived.Period >= new DateTime(1998, 1, 1)
                           join monthly in db.Payments.AsNoTracking().Where(p => p.ImportedFromJack && p.Amount != null)
                           on new { archived.SubscriberId, archived.Period.Year, archived.Period.Month }
                           equals new { monthly.SubscriberId, monthly.Year, monthly.Month }
                           select new { archived.Source, archived.Amount };
        var receipts = payments.Concat(jackReceipts)
            .GroupBy(p => p.Source == null || p.Source.Trim() == "" ? "не указан" : p.Source.Trim())
            .OrderBy(g => g.Key)
            .Select(g => new AccountingMonth.ReceiptLine(g.Key, g.Count(), g.Sum(p => p.Amount))).ToList();
        return new AccountingReport(shipment, receipts);
    }
}
