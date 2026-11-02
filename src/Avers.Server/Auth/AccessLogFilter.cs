using Avers.Data;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Auth;

/// <summary>
/// Журнал просмотров (152-ФЗ): запросы, которые отдают персональные данные абонентов, пишут, кто и чьи данные открыл.
/// Подключается к маршруту: <c>.LogsView("Карточка абонента", AccessLogFilter.Subscriber("id"))</c>.
/// </summary>
public static class AccessLogFilter
{
    public static RouteHandlerBuilder LogsView(this RouteHandlerBuilder builder, string action, Func<HttpContext, AversDbContext, string> target) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var db = http.RequestServices.GetRequiredService<AversDbContext>();
            var at = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            AccessLog.Add(db, at, ChangeLog.Who(db, http.User.AppUserId()), action, target(http, db));
            return await next(context);
        });

    /// <summary>Абонент по номеру из маршрута: «л/с 1932068, Иванова М.П., кв. 68».</summary>
    public static Func<HttpContext, AversDbContext, string> Subscriber(string route) => (http, db) =>
        int.TryParse(http.Request.RouteValues[route]?.ToString(), out var id)
        && db.Subscribers.AsNoTracking().FirstOrDefault(s => s.Id == id) is { } s
            ? ChangeLog.Account(s)
            : $"абонент № {http.Request.RouteValues[route]}";

    /// <summary>Дом по номеру из маршрута: «ул. Волочаевская, д. 19Д».</summary>
    public static Func<HttpContext, AversDbContext, string> House(string route) => (http, db) =>
        int.TryParse(http.Request.RouteValues[route]?.ToString(), out var id)
        && db.Houses.AsNoTracking().Include(h => h.Street).FirstOrDefault(h => h.Id == id) is { } h
            ? h.AddressLine
            : $"дом № {http.Request.RouteValues[route]}";

    /// <summary>Скан по номеру: владелец скана.</summary>
    public static Func<HttpContext, AversDbContext, string> Document(string route) => (http, db) =>
        int.TryParse(http.Request.RouteValues[route]?.ToString(), out var id)
        && db.SubscriberDocuments.AsNoTracking().Include(d => d.Subscriber).FirstOrDefault(d => d.Id == id) is { } d
            ? $"{d.Kind}, {d.Name} — {ChangeLog.Account(d.Subscriber)}"
            : $"скан № {http.Request.RouteValues[route]}";

    /// <summary>Что искали: значения строки запроса, например «Account: 1932068».</summary>
    public static string Query(HttpContext http, AversDbContext db) =>
        string.Join(", ", http.Request.Query.Where(q => !string.IsNullOrWhiteSpace(q.Value)).Select(q => $"{q.Key}: {q.Value}"));

    public static string Everyone(HttpContext http, AversDbContext db) => "все должники";
}
