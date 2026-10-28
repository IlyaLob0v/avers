using System.Collections.Concurrent;

namespace Avers.Server.Auth;

/// <summary>
/// Неверные пароли по логину, с любых адресов: после <see cref="Limit"/> за <see cref="Window"/> вход под этим логином
/// закрыт до конца окна. Лимит по адресу (Program.cs) не спасает, когда адрес можно подменить или сменить.
/// Считаются только существующие логины, поэтому словарь не растёт от выдуманных.
/// </summary>
public sealed class LoginFailures(TimeProvider time)
{
    public const int Limit = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset Since)> _failures = new();

    public bool IsLocked(string login) =>
        _failures.TryGetValue(login, out var f) && f.Count >= Limit && time.GetUtcNow() - f.Since < Window;

    public void Add(string login)
    {
        var now = time.GetUtcNow();
        _failures.AddOrUpdate(login, (1, now), (_, f) => now - f.Since < Window ? (f.Count + 1, f.Since) : (1, now));
    }

    public void Reset(string login) => _failures.TryRemove(login, out _);
}
