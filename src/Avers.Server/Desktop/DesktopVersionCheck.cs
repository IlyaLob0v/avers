using Avers.Contracts;

namespace Avers.Server.Desktop;

/// <summary>
/// Старое рабочее место (без заголовка версии или с меньшей) получает 426 и текст «Обновите программу».
/// Вход, страница механика и проверка здоровья версию не шлют и не проверяются.
/// </summary>
public static class DesktopVersionCheck
{
    public static void UseDesktopVersionCheck(this WebApplication app) => app.Use(async (http, next) =>
    {
        var path = http.Request.Path;
        var exempt = !path.StartsWithSegments("/api") || path.StartsWithSegments("/api/auth") || path.StartsWithSegments("/api/mobile");
        if (!exempt && (!int.TryParse(http.Request.Headers[DesktopClient.Header], out var version) || version < DesktopClient.Version))
        {
            await Results.Problem(
                detail: "Эта версия Аверса устарела. Обновите программу: спросите администратора.",
                statusCode: StatusCodes.Status426UpgradeRequired).ExecuteAsync(http);
            return;
        }
        await next(http);
    });
}
