namespace Avers.Domain;

public sealed class HouseScheme
{
    public int Id { get; set; }
    public int HouseId { get; set; }
    public House House { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Format { get; set; } = "SVG";
    public DateOnly UpdatedAt { get; set; }
}
