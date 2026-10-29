using Avers.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avers.Data;

public static class AversPaths
{
    public static string DataDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(root, "Avers");
        }
    }

    public static string DatabaseFile => Path.Combine(DataDirectory, "avers.db");

    /// <summary>Печать, выгрузки Excel и открытые сканы. Папка своя у каждого пользователя: на сервере xrdp общую /tmp видят все.</summary>
    public static string FilesDirectory => Path.Combine(DataDirectory, "Файлы");
}

/// <summary>Какая СУБД под моделью: PostgreSQL — рабочая база, SQLite — учебный режим и тесты.</summary>
public enum DatabaseProvider
{
    Postgres,
    Sqlite
}

public static class AversDataExtensions
{
    public static IServiceCollection AddAversData(this IServiceCollection services, string? databasePath = null)
    {
        var path = databasePath ?? AversPaths.DatabaseFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        services.AddDbContextFactory<AversDbContext>(options => options.UseSqlite($"Data Source={path}").AddInterceptors(SqliteUnicodeInterceptor.Instance));
        return services;
    }

    /// <summary>Подключение для сервера приложений: один контекст на запрос.</summary>
    public static IServiceCollection AddAversDbContext(this IServiceCollection services,
        DatabaseProvider provider, string connectionString)
    {
        services.AddDbContext<AversDbContext>(options => options.UseAvers(provider, connectionString));
        return services;
    }

    public static DbContextOptionsBuilder UseAvers(this DbContextOptionsBuilder options,
        DatabaseProvider provider, string connectionString) => provider switch
        {
            DatabaseProvider.Postgres => options.UseNpgsql(connectionString),
            DatabaseProvider.Sqlite => options.UseSqlite(connectionString).AddInterceptors(SqliteUnicodeInterceptor.Instance),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
}
