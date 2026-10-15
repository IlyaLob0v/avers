namespace Avers.Domain;

public sealed class YearPayment
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber Subscriber { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Tariff { get; set; }
    public string? Source { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? PaidAt { get; set; }

    /// <summary>Сотрудник, который внёс оплату в программу.</summary>
    public string? RecordedBy { get; set; }

    /// <summary>Кто оплатил, если не сам абонент (например, родственник по квитанции).</summary>
    public string? PayerName { get; set; }
}
