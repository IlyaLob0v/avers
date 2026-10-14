namespace Avers.Domain;

public sealed class UnifiedReceipt
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber Subscriber { get; set; } = null!;
    public string InnerCode { get; set; } = "";
    public string PzhreuIndex { get; set; } = "";
    public string Account { get; set; } = "";
    public DateOnly Moment { get; set; }
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public string NOs { get; set; } = "";
    public string NKass { get; set; } = "";
    public string NOper { get; set; } = "";
    public string NKvit { get; set; } = "";
}
