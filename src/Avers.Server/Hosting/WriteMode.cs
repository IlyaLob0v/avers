namespace Avers.Server.Hosting;

/// <summary>Ограничение рабочих записей на время сверки, пилота или возврата.</summary>
public static class WriteMode
{
    private sealed class PilotWrite;

    public static RouteHandlerBuilder AllowPilotWrite(this RouteHandlerBuilder route) =>
        route.WithMetadata(new PilotWrite());

    public static void UseWriteMode(this WebApplication app)
    {
        var mode = app.Configuration["Avers:WriteMode"] ?? "Full";
        if (mode == "Full")
            return;
        if (mode is not ("ReadOnly" or "Pilot"))
            throw new InvalidOperationException("Avers:WriteMode — Full, ReadOnly или Pilot.");

        app.Use(async (http, next) =>
        {
            var request = http.Request;
            var isWrite = request.Path.StartsWithSegments("/api") &&
                !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) &&
                !HttpMethods.IsOptions(request.Method) &&
                !(HttpMethods.IsPost(request.Method) && request.Path == "/api/auth/login");
            if (!isWrite || (mode == "Pilot" && http.GetEndpoint()?.Metadata.GetMetadata<PilotWrite>() is not null))
            {
                await next(http);
                return;
            }

            await Results.Problem(
                detail: mode == "ReadOnly" ? "В Аверсе временно разрешён только просмотр." :
                    "Во время пилота доступны только запись и отмена оплат и работа с заявками АДС.",
                statusCode: StatusCodes.Status409Conflict).ExecuteAsync(http);
        });
    }
}
