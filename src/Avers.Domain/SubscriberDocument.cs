namespace Avers.Domain;

/// <summary>Скан документа абонента для юристов: договор, решение суда, постановление ОСП.</summary>
public sealed class SubscriberDocument
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber Subscriber { get; set; } = null!;
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Format { get; set; } = "";
    public DateOnly AddedAt { get; set; }
    public string AddedBy { get; set; } = "";
    public byte[] Content { get; set; } = [];
}
