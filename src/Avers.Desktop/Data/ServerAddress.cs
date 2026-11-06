using System.Text.Json;

namespace Avers.Desktop.Data;

/// <summary>С чем открыть программу: общая база на сервере, учебная база (<c>--demo</c>) или сообщение об ошибке настройки.</summary>
public sealed record StartupMode(Uri? Server, bool Demo, string? Problem);

/// <summary>
/// Адрес сервера Аверса: ключ <c>--server=https://адрес</c> в ярлыке или файл <c>avers.json</c>
/// рядом с программой (<c>{ "server": "https://адрес" }</c>). Учебная база на этом компьютере — только ключом <c>--demo</c>:
/// без адреса программа не открывается, чтобы кассир не принимал оплаты в локальный файл.
/// </summary>
public static class ServerAddress
{
    public const string FileName = "avers.json";
    public const string DemoKey = "--demo";

    public static StartupMode Choose(IReadOnlyCollection<string> args, string? directory = null)
    {
        if (args.Contains(DemoKey, StringComparer.OrdinalIgnoreCase))
            return new StartupMode(null, true, null);
        try
        {
            return Find(args, directory) is { } server
                ? new StartupMode(server, false, null)
                : new StartupMode(null, false,
                    $"Не задан адрес сервера Аверса (файл {FileName} рядом с программой). Обратитесь к администратору.");
        }
        catch (InvalidOperationException e)
        {
            return new StartupMode(null, false, e.Message + " Обратитесь к администратору.");
        }
    }

    /// <summary>Обычный http:// к другому компьютеру: пароли и данные абонентов идут по сети открытым текстом.</summary>
    public static bool IsInsecure(Uri server) => server.Scheme == Uri.UriSchemeHttp && !server.IsLoopback;

    public static Uri? Find(IEnumerable<string> args, string? directory = null)
    {
        var value = args.Select(a => a.StartsWith("--server=", StringComparison.OrdinalIgnoreCase) ? a[9..] : null)
            .LastOrDefault(a => a is not null) ?? FromFile(Path.Combine(directory ?? AppContext.BaseDirectory, FileName));
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!Uri.TryCreate(value.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException($"Адрес сервера «{value}» не похож на https://адрес.");
        return uri;
    }

    private static string? FromFile(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("server", out var server) ? server.GetString() : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException($"В файле {FileName} ошибка, адрес сервера не прочитать.");
        }
    }
}
