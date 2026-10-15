using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Avers.Data;

/// <summary>
/// Встроенные lower()/upper() в SQLite меняют регистр только у латиницы, и поиск «авангард»
/// не находит «Авангардная». Подменяем их на .NET-версии, чтобы учебный режим искал так же, как PostgreSQL.
/// </summary>
public sealed class SqliteUnicodeInterceptor : DbConnectionInterceptor
{
    public static readonly SqliteUnicodeInterceptor Instance = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Register(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Register(connection);
        return Task.CompletedTask;
    }

    public static void Register(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
            return;
        sqlite.CreateFunction("lower", (string? s) => s?.ToLowerInvariant(), isDeterministic: true);
        sqlite.CreateFunction("upper", (string? s) => s?.ToUpperInvariant(), isDeterministic: true);
    }
}
