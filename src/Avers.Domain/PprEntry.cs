namespace Avers.Domain;

public sealed class PprEntry
{
    public int Id { get; set; }
    public int HouseId { get; set; }
    public House House { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public PprStatus Status { get; set; }
}
