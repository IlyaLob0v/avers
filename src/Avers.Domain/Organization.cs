namespace Avers.Domain;

public sealed class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phones { get; set; } = "";
    public string HeadName { get; set; } = "";
    public string HeadPhone { get; set; } = "";
    public string EngineerName { get; set; } = "";
    public string EngineerPhone { get; set; } = "";

    public ICollection<House> Houses { get; set; } = new List<House>();
}
