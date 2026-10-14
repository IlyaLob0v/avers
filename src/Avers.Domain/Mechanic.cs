namespace Avers.Domain;

public sealed class Mechanic
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Position { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
}
