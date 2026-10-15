namespace Avers.Domain;

public sealed class WorkTicket
{
    public int Id { get; set; }
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public int HouseId { get; set; }
    public House House { get; set; } = null!;
    public int? SubscriberId { get; set; }
    public Subscriber? Subscriber { get; set; }
    public string Apartment { get; set; } = "";
    public string Section { get; set; } = "";
    public string Phone { get; set; } = "";
    public TicketKind Kind { get; set; }
    public string Fault { get; set; } = "";
    public string WorkDone { get; set; } = "";
    public string Mechanic { get; set; } = "";
    public TicketStatus Status { get; set; }
    public string AcceptedBy { get; set; } = "";

    /// <summary>Кто принял заявку. По нему пользователь АДС видит, какие заявки он может править.</summary>
    public int? AcceptedByUserId { get; set; }

    /// <summary>Дата выполнения: ставится, когда заявка переходит в «выполнена».</summary>
    public DateOnly? DoneDate { get; set; }

    public string RisersNote { get; set; } = "";
    public string StatusLabel => Billing.StatusTitle(Status);
    public string KindLabel => Billing.KindTitle(Kind);
}
