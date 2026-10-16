using Avers.Data;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data.Tests;

/// <summary>
/// Модель строится и для PostgreSQL. Подключение не нужно: EF только генерирует DDL.
/// </summary>
public class PostgresModelTests
{
    private static AversDbContext Context()
    {
        var options = new DbContextOptionsBuilder<AversDbContext>();
        options.UseAvers(DatabaseProvider.Postgres, "Host=localhost;Database=avers;Username=avers");
        return new AversDbContext(options.Options);
    }

    [Fact]
    public void CreateScript_KeepsMoneyAsNumericAndDatesAsDate()
    {
        using var db = Context();
        var script = db.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE \"Subscribers\"", script);
        Assert.Contains("\"Amount\" numeric(12,2)", script);
        Assert.Contains("\"PaidAt\" date", script);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Payments_SubscriberId_Year_Month\"", script);
    }

    [Fact]
    public void Migrations_MatchModel()
    {
        using var db = Context();

        Assert.False(db.Database.HasPendingModelChanges(),
            "Модель поменялась без миграции: dotnet ef migrations add <Название> --project src/Avers.Data --output-dir Migrations");
    }
}
