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

var builder = WebApplication.CreateBuilder(args);

builder.AddAversDatabase();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(o => o.BearerTokenExpiration = TimeSpan.FromHours(12));
builder.Services.AddAuthorization(Policies.Add);
builder.Services.AddSingleton<IAuthorizationHandler, AversAuthorizationHandler>();

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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuth();
app.MapUsers();
app.MapSubscribers();
app.MapTickets();
app.MapDesktop();
app.MapPpr();
app.MapMobile();

await app.InitializeDatabaseAsync();
if (AdminCommands.TryRun(args, out var command))
    return await command(app.Services);
await app.RunAsync();
return 0;

/// <summary>Точка входа; открыта для интеграционных тестов (WebApplicationFactory).</summary>
public partial class Program;
