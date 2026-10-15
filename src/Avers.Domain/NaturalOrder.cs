namespace Avers.Domain;

/// <summary>Номера домов и квартир по-человечески: 2, 9, 10, 10а, 12/1 (а не 10, 12/1, 2, 9).</summary>
public sealed class NaturalOrder : IComparer<string>
{
    public static readonly NaturalOrder Instance = new();

    public int Compare(string? x, string? y)
    {
        var (nx, rx) = Split(x ?? "");
        var (ny, ry) = Split(y ?? "");
        var byNumber = nx.CompareTo(ny);
        return byNumber != 0 ? byNumber : string.Compare(rx, ry, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Number, string Suffix) Split(string s)
    {
        var digits = s.TakeWhile(char.IsDigit).Count();
        var number = digits > 0 && int.TryParse(s.AsSpan(0, Math.Min(digits, 9)), out var n) ? n : int.MaxValue;
        return (number, s[digits..]);
    }
}
