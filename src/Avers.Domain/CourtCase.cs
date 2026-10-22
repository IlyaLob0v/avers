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
    public decimal StateDuty { get; set; }

    /// <summary>Куда направлен исполнительный документ: ОСП или ОСФР (пенсионный).</summary>
    public string WritSentTo { get; set; } = "";
    public DateOnly? WritSentDate { get; set; }

    /// <summary>Поступления по делу, как в таблице юристов: «901,58-28.11.24 + 214,93-24.12.24».</summary>
    public string Receipts { get; set; } = "";
    public string Notes { get; set; } = "";
}
