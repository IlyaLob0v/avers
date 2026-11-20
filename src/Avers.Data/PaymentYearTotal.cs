namespace Avers.Data;

/// <summary>Производные суммы рабочих месяцев. PostgreSQL обновляет их в транзакции изменения Payments.</summary>
public sealed class PaymentYearTotal
{
    public int SubscriberId { get; set; }
    public int Year { get; set; }
    public decimal Charged { get; set; }
    public decimal Paid { get; set; }
    public int Unpaid { get; set; }
}
