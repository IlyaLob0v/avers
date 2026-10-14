namespace Avers.Domain;

public sealed class Street
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public ICollection<House> Houses { get; set; } = new List<House>();
}
