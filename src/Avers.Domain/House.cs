namespace Avers.Domain;

public sealed class House
{
    public int Id { get; set; }
    public int StreetId { get; set; }
    public Street Street { get; set; } = null!;
    public string Number { get; set; } = "";
    public string PzhreuIndex { get; set; } = "";
    public string PzhreuAccount { get; set; } = "";
    public int Floors { get; set; }
    public int Entrances { get; set; }
    public int Risers { get; set; }
    public int Apartments { get; set; }
    public string Zone { get; set; } = "";
    public bool Serviced { get; set; } = true;
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string Antennas { get; set; } = "";
    public string AerialCables { get; set; } = "";
    public string Amplifiers { get; set; } = "";
    public string PowerSupplies { get; set; } = "";
    public string TechFloorKey { get; set; } = "";
    public string OtherNotes { get; set; } = "";
    public DateOnly? LastPpr { get; set; }

    public string? OpenFault { get; set; }
    public string? OpenFaultReason { get; set; }
    public DateOnly? OpenFaultPlannedFix { get; set; }

    public ICollection<Subscriber> Subscribers { get; set; } = new List<Subscriber>();
    public ICollection<WorkTicket> Tickets { get; set; } = new List<WorkTicket>();
    public ICollection<HouseScheme> Schemes { get; set; } = new List<HouseScheme>();
    public ICollection<PprEntry> Ppr { get; set; } = new List<PprEntry>();
    public ICollection<IntercomPanel> Intercoms { get; set; } = new List<IntercomPanel>();

    public string AddressLine => Street is null ? $"д. {Number}" : $"ул. {Street.Name}, д. {Number}";
}
