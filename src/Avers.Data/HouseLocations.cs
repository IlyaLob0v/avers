using System.Text.Json;
using System.Text.RegularExpressions;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Точки домов для офлайн-карты. Адреса берутся из выгрузки OpenStreetMap (ответ Overpass в JSON), дом ставится на карту,
/// если совпали улица и номер. Дома, которые уже стоят на карте (в том числе поставленные вручную), не трогаются.
/// </summary>
public static partial class HouseLocations
{
    public sealed record AddressPoint(string Street, string Number, double Latitude, double Longitude);

    public sealed record ImportResult(int Placed, IReadOnlyList<string> NotFound);

    private static readonly HashSet<string> StreetTypes =
    [
        "улица", "ул", "проспект", "просп", "пр-т", "пр", "переулок", "пер", "бульвар", "б-р", "бул", "проезд", "пр-д",
        "шоссе", "ш", "площадь", "пл", "тупик", "туп", "набережная", "наб", "аллея", "микрорайон", "мкр", "квартал", "кв-л"
    ];

    /// <summary>Улица без типа, регистра, «ё» и порядка слов: «1-я Северная улица» = «Северная 1-я».</summary>
    public static string StreetKey(string name) =>
        string.Join(' ', NonWord().Split(name.ToLowerInvariant().Replace('ё', 'е'))
            .Where(w => w.Length > 0 && !StreetTypes.Contains(w)).Order(StringComparer.Ordinal));

    /// <summary>Номер дома без пробелов и регистра, латинские буквы-двойники — русскими, «корпус» — «к».</summary>
    public static string NumberKey(string number)
    {
        var key = number.ToLowerInvariant().Replace('ё', 'е').Replace("корпус", "к").Replace("корп.", "к").Replace("корп", "к");
        return new string(key.Where(c => !char.IsWhiteSpace(c)).Select(c => c switch
        {
            'a' => 'а',
            'e' => 'е',
            'o' => 'о',
            'c' => 'с',
            'k' => 'к',
            'p' => 'р',
            'x' => 'х',
            _ => c
        }).ToArray());
    }

    /// <summary>Адреса из ответа Overpass (<c>out tags center</c>): точки и центры зданий. «44;46» — два адреса.</summary>
    public static IEnumerable<AddressPoint> ReadOverpass(Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var e in doc.RootElement.GetProperty("elements").EnumerateArray())
        {
            if (!e.TryGetProperty("tags", out var tags)
                || !tags.TryGetProperty("addr:street", out var street) || !tags.TryGetProperty("addr:housenumber", out var numbers))
                continue;
            var at = e.TryGetProperty("center", out var center) ? center : e;
            if (!at.TryGetProperty("lat", out var lat) || !at.TryGetProperty("lon", out var lon))
                continue;
            foreach (var number in numbers.GetString()!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return new AddressPoint(street.GetString()!, number, lat.GetDouble(), lon.GetDouble());
        }
    }

    /// <summary>
    /// Ставит на карту дома без точки. Если две улицы Аверса отличаются только типом («Ленина ул.» и «Ленина пр.»),
    /// их дома остаются без точки: такие лучше поставить вручную, чем перепутать.
    /// </summary>
    public static ImportResult Import(AversDbContext db, IEnumerable<AddressPoint> points, DateTimeOffset at)
    {
        var byAddress = new Dictionary<(string, string), AddressPoint>();
        foreach (var p in points)
            byAddress.TryAdd((StreetKey(p.Street), NumberKey(p.Number)), p);

        var houses = db.Houses.Include(h => h.Street).ToList();
        var ambiguous = houses.Select(h => h.Street).Distinct().GroupBy(s => StreetKey(s.Name))
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

        var placed = 0;
        var notFound = new List<string>();
        foreach (var h in houses.Where(h => h.Latitude is null || h.Longitude is null)
                     .OrderBy(h => h.Street.Name).ThenBy(h => h.Number, NaturalOrder.Instance))
        {
            var street = StreetKey(h.Street.Name);
            if (!ambiguous.Contains(street) && byAddress.TryGetValue((street, NumberKey(h.Number)), out var p))
            {
                (h.Latitude, h.Longitude) = (p.Latitude, p.Longitude);
                placed++;
            }
            else
                notFound.Add(h.AddressLine);
        }

        ChangeLog.Add(db, at, "команда avers-map", "Карта", "дома без точки на карте",
            placed == 0 ? [] : [$"Поставлено на карту: {placed}", $"Не найдено: {notFound.Count}"]);
        db.SaveChanges();
        return new ImportResult(placed, notFound);
    }

    [GeneratedRegex(@"[\s.,()«»""]+")]
    private static partial Regex NonWord();
}
