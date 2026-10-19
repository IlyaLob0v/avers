namespace Avers.Domain;

/// <summary>Итоги месяца для бухгалтерии и 1С: отгрузка (начисления абонентской платы) и поступления по источникам.</summary>
public static class AccountingMonth
{
    public sealed record ShipmentLine(decimal Tariff, int Count, decimal Sum);

    public sealed record ReceiptLine(string Source, int Count, decimal Sum);

    /// <summary>Начисления за месяц, сгруппированные по тарифу: количество × цена, как в документе «Реализация».</summary>
    public static List<ShipmentLine> Shipment(IEnumerable<YearPayment> payments, int year, int month) =>
        payments.Where(p => p.Year == year && p.Month == month && p.Tariff > 0)
            .GroupBy(p => p.Tariff)
            .OrderBy(g => g.Key)
            .Select(g => new ShipmentLine(g.Key, g.Count(), g.Sum(p => p.Tariff)))
            .ToList();

    /// <summary>Оплаты, поступившие в этом месяце (по дате оплаты, за любой расчётный месяц), по источникам.</summary>
    public static List<ReceiptLine> Receipts(IEnumerable<YearPayment> payments, int year, int month) =>
        payments.Where(p => p.Amount is not null && p.PaidAt is { } d && d.Year == year && d.Month == month)
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Source) ? "не указан" : p.Source.Trim())
            .OrderBy(g => g.Key)
            .Select(g => new ReceiptLine(g.Key, g.Count(), g.Sum(p => p.Amount!.Value)))
            .ToList();
}
