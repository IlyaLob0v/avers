using Avers.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Avers.Desktop.Tests;

public class DemoDatabaseTests
{
    [Fact]
    public void SchemaChange_PreservesOldDatabaseBeforeReset()
    {
        var file = Path.Combine(Path.GetTempPath(), $"avers-backup-test-{Guid.NewGuid():N}.db");
        try
        {
            using var db = new AversDbContext(new DbContextOptionsBuilder<AversDbContext>().UseSqlite($"Data Source={file};Pooling=False").Options);
            DemoDatabase.EnsureCurrent(db, file);
            db.Database.OpenConnection();
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL");
            db.Database.ExecuteSqlRaw("CREATE TABLE retained (value TEXT); INSERT INTO retained VALUES ('ручной ввод')");
            File.WriteAllText(file + ".schema", "прежняя схема");

            DemoDatabase.EnsureCurrent(db, file);

            var backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(file)!, Path.GetFileName(file) + ".backup-*.db"));
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM retained";
            Assert.Equal("ручной ввод", command.ExecuteScalar());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in Directory.GetFiles(Path.GetDirectoryName(file)!, Path.GetFileName(file) + "*")) File.Delete(path);
        }
    }

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
