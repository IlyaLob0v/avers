using Avers.Data;
using Avers.Server.Hosting;

namespace Avers.Server.Tests;

public class DatabaseOptionsTests
{
    [Fact]
    public void SeedDemo_OnPostgres_GivesWarning()
    {
        var options = new DatabaseOptions { Provider = DatabaseProvider.Postgres, SeedDemo = true };

        Assert.Contains("учебные", DatabaseSetup.StartupWarning(options));
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres, false)]
    [InlineData(DatabaseProvider.Sqlite, true)]
    public void NoWarning_WithoutDemoOrInDemoMode(DatabaseProvider provider, bool seedDemo) =>
        Assert.Null(DatabaseSetup.StartupWarning(new DatabaseOptions { Provider = provider, SeedDemo = seedDemo }));
}
