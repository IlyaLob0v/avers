namespace Avers.Data;

/// <summary>Подтверждённая кассовая операция. Хранится и после отмены оплаты, чтобы повтор не создал её заново.</summary>
public sealed class PaymentOperation
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public int SubscriberId { get; set; }
    public string Request { get; set; } = "";
    public string Receipt { get; set; } = "";
}
