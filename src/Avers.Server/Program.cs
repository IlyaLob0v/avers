using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Avers.Server.Auth;
using Avers.Server.Desktop;
using Avers.Server.Hosting;
using Avers.Server.Mobile;
using Avers.Server.Ppr;
using Avers.Server.Subscribers;
using Avers.Server.Tickets;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.AddAversDatabase();
builder.Services.AddSingleton(TimeProvider.System);

// Ключи, которыми подписаны входы. Без папки они живут в памяти, и после перезапуска сервера все входят заново.
if (builder.Configuration["Avers:KeysDirectory"] is { Length: > 0 } keys)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys));

builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(o => o.BearerTokenExpiration = TimeSpan.FromHours(12));
builder.Services.AddAuthorization(Policies.Add);
builder.Services.AddSingleton<IAuthorizationHandler, AversAuthorizationHandler>();
builder.Services.AddSingleton<LoginFailures>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Из интернета запросы приходят через Caddy на этом же сервере (deploy/server/avers-internet):
// адрес телефона берём из X-Forwarded-For, иначе лимит попыток входа был бы один на всех.
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor });
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseFreshUser();
app.UseAuthorization();
app.UseDesktopVersionCheck();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuth();
app.MapUsers();
app.MapSubscribers();
app.MapTickets();
app.MapDesktop();
app.MapSchemes();
app.MapDocuments();
app.MapIntercoms();
app.MapPhonebook();
app.MapPpr();
app.MapMobile();

await app.InitializeDatabaseAsync();
if (AdminCommands.TryRun(args, out var command))
    return await command(app.Services);
await app.RunAsync();
return 0;

/// <summary>Точка входа; открыта для интеграционных тестов (WebApplicationFactory).</summary>
public partial class Program;
