using System.Text;
using Avers.Domain;

namespace Avers.Data.Tests;

public class HouseLocationsTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("улица Красный Путь", "Красный Путь")]
    [InlineData("Красный Путь улица", "красный путь")]
    [InlineData("1-я Северная улица", "Северная 1-я")]
    [InlineData("проспект Карла Маркса", "Карла Маркса пр.")]
    [InlineData("улица Ёлочная", "Елочная")]
    public void StreetKey_IgnoresTypeOrderCaseAndYo(string osm, string avers) =>
        Assert.Equal(HouseLocations.StreetKey(avers), HouseLocations.StreetKey(osm));

    [Fact]
    public void StreetKey_KeepsLineNumber() =>
        Assert.NotEqual(HouseLocations.StreetKey("1-я Линия"), HouseLocations.StreetKey("2-я Линия"));

    [Theory]
    [InlineData("19 А", "19А")]
    [InlineData("19a", "19А")]
    [InlineData("12 корпус 1", "12 к1")]
    [InlineData("12 корп. 1", "12к1")]
    public void NumberKey_IgnoresSpacesLatinLettersAndBuildingWord(string osm, string avers) =>
        Assert.Equal(HouseLocations.NumberKey(avers), HouseLocations.NumberKey(osm));

    [Fact]
    public void ReadOverpass_TakesNodesAndBuildingCentres_AndSplitsNumberLists()
    {
        const string json = """
            {"elements":[
              {"type":"node","id":1,"lat":55.01,"lon":73.31,"tags":{"addr:street":"Волочаевская улица","addr:housenumber":"19"}},
              {"type":"way","id":2,"center":{"lat":55.02,"lon":73.32},"tags":{"addr:street":"улица Ленина","addr:housenumber":"44;46","building":"yes"}},
              {"type":"way","id":3,"tags":{"addr:street":"улица Ленина","addr:housenumber":"48"}}
            ]}
            """;

        var points = HouseLocations.ReadOverpass(new MemoryStream(Encoding.UTF8.GetBytes(json))).ToList();

        Assert.Equal(
            [("Волочаевская улица", "19", 55.01, 73.31), ("улица Ленина", "44", 55.02, 73.32), ("улица Ленина", "46", 55.02, 73.32)],
            points.Select(p => (p.Street, p.Number, p.Latitude, p.Longitude)));
    }

    [Fact]
    public void Import_PlacesMatchedHouses_KeepsManualPoints_ReportsTheRest()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        var volochaevskaya = new Street { Name = "Волочаевская" };
        db.Houses.AddRange(
            new House { Street = volochaevskaya, Number = "19А" },
            new House { Street = volochaevskaya, Number = "21", Latitude = 1, Longitude = 2 },
            new House { Street = new Street { Name = "Тарская" }, Number = "5" });
        db.SaveChanges();

        var result = HouseLocations.Import(db,
        [
            new("Волочаевская улица", "19а", 55.01, 73.31),
            new("Волочаевская улица", "21", 55.02, 73.32)
        ], At);

        Assert.Equal(1, result.Placed);
        Assert.Equal(["ул. Тарская, д. 5"], result.NotFound);
        var houses = db.Houses.OrderBy(h => h.Number).Select(h => new { h.Number, h.Latitude, h.Longitude }).ToList();
        Assert.Equal((55.01, 73.31), (houses[0].Latitude, houses[0].Longitude));
        Assert.Equal((1d, 2d), (houses[1].Latitude, houses[1].Longitude));
        Assert.Single(db.ChangeLog);
    }

    [Fact]
    public void Import_SkipsStreetsThatDifferOnlyByType()
    {
        using var database = new SqliteDatabase();
        using var db = database.CreateContext();
        db.Houses.AddRange(
            new House { Street = new Street { Name = "Ленина ул." }, Number = "1" },
            new House { Street = new Street { Name = "Ленина пр." }, Number = "1" });
        db.SaveChanges();

        var result = HouseLocations.Import(db, [new("улица Ленина", "1", 55, 73)], At);

        Assert.Equal(0, result.Placed);
        Assert.Equal(2, result.NotFound.Count);
    }
}
