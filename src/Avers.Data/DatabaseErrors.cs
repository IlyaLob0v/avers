using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Avers.Data;

public static class DatabaseErrors
{
    public const string LinkedDataMessage = "Связанные данные изменились. Обновите сведения и повторите действие.";

    public static bool IsReferenceConflict(DbUpdateException exception) => exception.InnerException is
        PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation }
        or SqliteException { SqliteExtendedErrorCode: 787 }
        || exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1811 } sqlite
            && sqlite.Message.Contains("AVERS_DELETE_LINKED_DATA", StringComparison.Ordinal);
}
