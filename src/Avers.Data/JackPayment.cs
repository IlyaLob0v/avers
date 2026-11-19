namespace Avers.Data;

/// <summary>Исходная операция JACK для сверки и разбивки поступлений по дате и источнику.</summary>
public sealed class JackPayment
{
    public long Id { get; set; }
    public int SubscriberId { get; set; }
    public DateTime Period { get; set; }
    public DateTime PaidAt { get; set; }
    public decimal Amount { get; set; }
    public int PayTypeId { get; set; }
    public int ConnectionTypeId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Source { get; set; }
    public string? RecordedBy { get; set; }
}
