using Microsoft.Data.Sqlite;

namespace Avers.Desktop.Tests;

public class DemoDatabaseTests
{
    [Fact]
    public void WorkDate_IsToday()
    {
        var file = Path.Combine(Path.GetTempPath(), $"avers-today-test-{Guid.NewGuid():N}.db");
        try
        {
            var vm = App.CreateMainViewModel(file);

            Assert.Equal(DateTime.Now.ToString("dd.MM.yyyy"), vm.WorkDateText);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(file);
            File.Delete(file + ".schema");
            File.Delete(file + ".lock");
        }
    }

    [Fact]
    public void SecondInstanceWaitsForDatabaseLock()
    {
        var file = Path.Combine(Path.GetTempPath(), $"avers-lock-test-{Guid.NewGuid():N}.db");
        try
        {
            using (DemoDatabase.Lock(file))
            {
                Assert.Throws<IOException>(() => DemoDatabase.Lock(file, TimeSpan.FromMilliseconds(300)));
            }

            using (DemoDatabase.Lock(file, TimeSpan.Zero)) { }
        }
        finally
        {
            File.Delete(file + ".lock");
        }
    }
}
