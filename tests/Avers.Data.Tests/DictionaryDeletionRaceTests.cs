using System.Text.RegularExpressions;
using Avers.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Avers.Data.Tests;

public class DictionaryDeletionRaceTests
{
    [Theory]
    [InlineData("subscriber")]
    [InlineData("ticket")]
    [InlineData("scheme")]
    [InlineData("ppr")]
    [InlineData("intercom")]
    [InlineData("intercomTicket")]
    public void HouseDeletion_PreservesChildCreatedAfterChecks(string kind) => HouseRace(kind, legacy: false);

    [Theory]
    [InlineData("subscriber")]
    [InlineData("ticket")]
    [InlineData("scheme")]
    [InlineData("ppr")]
    [InlineData("intercom")]
    [InlineData("intercomTicket")]
    public void ExistingSqliteUpgrade_PreservesChildCreatedAfterChecks(string kind) => HouseRace(kind, legacy: true);

    private static void HouseRace(string kind, bool legacy)
    {
        using var database = new RaceDatabase(legacy);
        int houseId;
        using (var seed = database.Open())
        {
            var house = new House { Number = "1", Street = new Street { Name = "Проверочная" } };
            seed.Houses.Add(house);
            seed.SaveChanges();
            houseId = house.Id;
        }
        var child = kind switch
        {
            "subscriber" => (object)new Subscriber
            {
                HouseId = houseId,
                Account = "race",
                LastName = "Проверка",
                Payments = [new YearPayment { Year = 2026, Month = 11, Tariff = 100, Amount = 100, PaidAt = new(2026, 11, 24) }]
            },
            "ticket" => new WorkTicket { HouseId = houseId, Number = 1, Date = new(2026, 11, 24) },
            "scheme" => new HouseScheme { HouseId = houseId, Name = "Схема" },
            "ppr" => new PprEntry { HouseId = houseId, Year = 2026, Month = 11 },
            "intercom" => new IntercomPanel { HouseId = houseId, Entrance = 1 },
            _ => new IntercomTicket { HouseId = houseId, Entrance = 1, Date = new(2026, 11, 24) }
        };
        var childId = 0;
        using var deleting = database.Open(new BeforeSave(() =>
        {
            using var colleague = database.Open();
            colleague.Add(child);
            colleague.SaveChanges();
            childId = (int)colleague.Entry(child).Property("Id").CurrentValue!;
        }));

        var error = Assert.Throws<DbUpdateException>(() => Phonebook.DeleteHouse(deleting, houseId, "Проверка", DateTimeOffset.UtcNow, 0));

        Assert.Equal(legacy ? 1811 : 787, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
        Assert.True(DatabaseErrors.IsReferenceConflict(error));
        if (legacy) Assert.Contains("AVERS_DELETE_LINKED_DATA", error.InnerException.Message);
        using var check = database.Open();
        Assert.NotNull(check.Houses.Find(houseId));
        Assert.NotNull(check.Find(child.GetType(), childId));
        if (kind == "subscriber") Assert.Equal(100, check.Payments.Single().Amount);
        Assert.Empty(check.ChangeLog);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StreetDeletion_PreservesHouseCreatedAfterChecks(bool legacy)
    {
        using var database = new RaceDatabase(legacy);
        int streetId;
        using (var seed = database.Open())
        {
            var street = new Street { Name = "Проверочная" };
            seed.Streets.Add(street);
            seed.SaveChanges();
            streetId = street.Id;
        }
        using var deleting = database.Open(new BeforeSave(() =>
        {
            using var colleague = database.Open();
            colleague.Houses.Add(new House { StreetId = streetId, Number = "1" });
            colleague.SaveChanges();
        }));

        var error = Assert.Throws<DbUpdateException>(() => Phonebook.DeleteStreet(deleting, streetId, "Проверка", DateTimeOffset.UtcNow));

        Assert.Equal(legacy ? 1811 : 787, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
        Assert.True(DatabaseErrors.IsReferenceConflict(error));
        if (legacy) Assert.Contains("AVERS_DELETE_LINKED_DATA", error.InnerException.Message);
        using var check = database.Open();
        Assert.NotNull(check.Streets.Find(streetId));
        Assert.Single(check.Houses);
        Assert.Empty(check.ChangeLog);
    }

    private sealed class BeforeSave(Action action) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            action();
            return result;
        }
    }

    private sealed class RaceDatabase : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DbContextOptions<AversDbContext> _options;

        public RaceDatabase(bool legacy)
        {
            _connection.Open();
            SqliteUnicodeInterceptor.Register(_connection);
            _options = new DbContextOptionsBuilder<AversDbContext>().UseSqlite(_connection).Options;
            using var db = Open();
            var schema = db.Database.GenerateCreateScript();
            // Старая серверная SQLite-база имеет CASCADE, даже после обновления модели приложения.
            if (legacy) schema = Regex.Replace(schema, "(REFERENCES \"(?:Houses|Streets)\" \\(\"Id\"\\))(?: ON DELETE (?:RESTRICT|CASCADE|NO ACTION))?", "$1 ON DELETE CASCADE");
            db.Database.ExecuteSqlRaw(schema);
            if (legacy)
            {
                SqliteSchema.EnsureRevisions(db);
                SqliteSchema.EnsureRevisions(db);
            }
        }

        public AversDbContext Open(params IInterceptor[] interceptors) =>
            new(new DbContextOptionsBuilder<AversDbContext>(_options).AddInterceptors(interceptors).Options);

        public void Dispose() => _connection.Dispose();
    }
}
