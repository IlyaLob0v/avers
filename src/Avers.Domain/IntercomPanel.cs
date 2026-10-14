namespace Avers.Domain;

public sealed class IntercomPanel
{
    public int Id { get; set; }
    public int HouseId { get; set; }
    public House House { get; set; } = null!;
    public int Entrance { get; set; }
    public string PanelType { get; set; } = "";
    public string SwitchBlock { get; set; } = "";
    public string Keys { get; set; } = "";
    public string Power { get; set; } = "";
}

public sealed class IntercomTicket
{
    public int Id { get; set; }
    public int HouseId { get; set; }
    public House House { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int Entrance { get; set; }
    public string Fault { get; set; } = "";
    public string Mechanic { get; set; } = "";
    public bool Done { get; set; }
}
