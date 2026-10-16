using Avers.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Avers.Server.Hosting;

/// <summary>Раздел <c>Avers:Database</c> в appsettings.json.</summary>
public sealed class DatabaseOptions
{
    public const string Section = "Avers:Database";

    /// <summary>Postgres — рабочая база, Sqlite — учебный режим.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;

    /// <summary>При запуске довести схему PostgreSQL до последней миграции EF.</summary>
    public bool Migrate { get; set; } = true;

    /// <summary>Создать таблицы SQLite без миграций. Только для учебного режима и тестов.</summary>
    public bool EnsureCreated { get; set; }

    /// <summary>Залить учебные данные Омска (пользователи admin/avers и т.д.). На рабочем сервере — никогда.</summary>
    public bool SeedDemo { get; set; }
}

public static class DatabaseSetup
{
    public static void AddAversDatabase(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new();
        var connectionString = builder.Configuration.GetConnectionString("Avers");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Не задана строка подключения ConnectionStrings:Avers. Пароль к базе храните в user-secrets " +
                "или в переменной окружения ConnectionStrings__Avers, не в appsettings.json.");

        builder.Services.AddSingleton(options);
        builder.Services.AddAversDbContext(options.Provider, connectionString);
        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<DatabaseOptions>();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AversDbContext>();
        if (options.Provider == DatabaseProvider.Postgres && options.Migrate)
            await db.Database.MigrateAsync();
        else if (options.EnsureCreated)
            await db.Database.EnsureCreatedAsync();
        if (options.SeedDemo)
            DemoSeeder.EnsureSeeded(db);
    }
}

public sealed class DatabaseHealthCheck(AversDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Нет связи с базой данных.");
}

public static class TimeProviderExtensions
{
    /// <summary>Сегодняшняя дата по часам сервера.</summary>
    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}
