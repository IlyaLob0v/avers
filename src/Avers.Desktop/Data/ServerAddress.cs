using System.Text.Json;

namespace Avers.Desktop.Data;

/// <summary>
/// Адрес сервера Аверса: ключ <c>--server=http://адрес:5115</c> в ярлыке или файл <c>avers.json</c>
/// рядом с программой (<c>{ "server": "http://адрес:5115" }</c>). Нет адреса — учебная база на этом компьютере.
/// </summary>
public static class ServerAddress
{
    public const string FileName = "avers.json";

    public static Uri? Find(IEnumerable<string> args, string? directory = null)
    {
        var value = args.Select(a => a.StartsWith("--server=", StringComparison.OrdinalIgnoreCase) ? a[9..] : null)
            .LastOrDefault(a => a is not null) ?? FromFile(Path.Combine(directory ?? AppContext.BaseDirectory, FileName));
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!Uri.TryCreate(value.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException($"Адрес сервера «{value}» не похож на http://адрес:порт.");
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
        catch (JsonException)
        {
            throw new InvalidOperationException($"В файле {FileName} ошибка, адрес сервера не прочитать.");
        }
    }
}
