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
}

public static class AversDataExtensions
{
    public static IServiceCollection AddAversData(this IServiceCollection services, string? databasePath = null)
    {
        var path = databasePath ?? AversPaths.DatabaseFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        services.AddDbContextFactory<AversDbContext>(options => options.UseSqlite($"Data Source={path}"));
        return services;
    }
}
