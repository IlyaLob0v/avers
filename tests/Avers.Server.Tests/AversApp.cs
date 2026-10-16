using System.Net.Http.Headers;
using System.Net.Http.Json;
using Avers.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Avers.Server.Tests;

/// <summary>
/// Сервер целиком в памяти процесса: своя временная база SQLite с учебными данными на каждый тест
/// и часы, остановленные на рабочей дате учебной базы.
/// </summary>
public sealed class AversApp : WebApplicationFactory<Program>
{
    public static readonly DateOnly Today = new(2026, 8, 31);

    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"avers-server-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Avers", $"Data Source={_databaseFile};Pooling=False");
        builder.UseSetting("Avers:Database:Provider", "Sqlite");
        builder.UseSetting("Avers:Database:EnsureCreated", "true");
        builder.UseSetting("Avers:Database:SeedDemo", "true");
        builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedTime(Today))));
    }

    /// <summary>Клиент, уже вошедший под учебным пользователем (пароль у всех — avers).</summary>
    public async Task<HttpClient> LoginAsync(string login)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password = "avers" });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<AversDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AversDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(_databaseFile);
    }

    private sealed record TokenResponse(string AccessToken);

    private sealed class FixedTime(DateOnly today) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
    }
}
