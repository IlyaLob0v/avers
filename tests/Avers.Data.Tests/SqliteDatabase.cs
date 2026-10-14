using Avers.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data.Tests;

/// <summary>
/// Пустая база SQLite в памяти на один тест: схема та же, что у оболочки.
/// </summary>
public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AversDbContext> _options;

    public SqliteDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AversDbContext>().UseSqlite(_connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AversDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
