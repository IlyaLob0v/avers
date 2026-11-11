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
using Microsoft.AspNetCore.StaticFiles;

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
    // Из интернета — 10 входов в минуту с адреса. В офисе сотрудники Windows 7/8 входят через xrdp на сервере,
    // то есть все с одного адреса, поэтому там лимит шире; подбор пароля сдерживает LoginFailures.
    o.AddPolicy(AuthEndpoints.LoginRateLimit, http =>
    {
        var internet = http.Request.Headers.ContainsKey(AuthEndpoints.InternetHeader);
        return RateLimitPartition.GetFixedWindowLimiter(
            (internet ? "internet:" : "office:") + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = internet ? 10 : 60, Window = TimeSpan.FromMinutes(1) });
    });
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
// Глифы подписей офлайн-карты (.pbf) — тип, которого нет в списке по умолчанию.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".pbf"] = "application/x-protobuf";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });
app.UseAuthentication();
app.UseFreshUser();
app.UseAuthorization();
app.UseDesktopVersionCheck();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();

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
app.MapMap();

await app.InitializeDatabaseAsync();
if (AdminCommands.TryRun(args, out var command))
    return await command(app.Services);
await app.RunAsync();
return 0;

/// <summary>Точка входа; открыта для интеграционных тестов (WebApplicationFactory).</summary>
public partial class Program;
