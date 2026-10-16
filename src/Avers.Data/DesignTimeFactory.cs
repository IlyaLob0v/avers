using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Avers.Data;

/// <summary>
/// Для <c>dotnet ef migrations add</c>: миграции пишутся под PostgreSQL, база для этого не нужна.
/// Учебная SQLite-база миграции не применяет, её таблицы создаёт EnsureCreated.
/// </summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AversDbContext>
{
    public AversDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AversDbContext>().UseNpgsql("Host=localhost;Database=avers").Options);
}
