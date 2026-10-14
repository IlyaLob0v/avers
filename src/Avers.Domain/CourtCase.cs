namespace Avers.Domain;

public sealed class CourtCase
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber Subscriber { get; set; } = null!;
    public string DelaAversId { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public DateOnly OrderDate { get; set; }
    public string Court { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}
