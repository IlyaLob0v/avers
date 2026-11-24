using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Avers.Data;

/// <summary>Версии записей и защита связанных данных существующей учебной базы сервера без её перестройки.</summary>
public static class SqliteSchema
{
    public static void EnsureRevisions(AversDbContext db)
    {
        if (!db.Database.IsSqlite()) return;
        using var transaction = db.Database.BeginTransaction();
        foreach (var table in new[] { "Houses", "Organizations", "Payments" })
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            var columns = new List<string>();
            using (var reader = command.ExecuteReader())
                while (reader.Read()) columns.Add(reader.GetString(1));
            if (columns.Count > 0 && !columns.Contains("Revision"))
            {
                command.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"Revision\" INTEGER NOT NULL DEFAULT 0";
                command.ExecuteNonQuery();
            }
        }
        // В старой SQLite остаются каскадные FK: триггеры проверяют связи атомарно с DELETE.
        db.Database.ExecuteSqlRaw("""
            CREATE TRIGGER IF NOT EXISTS "avers_house_delete_guard"
            BEFORE DELETE ON "Houses"
            WHEN EXISTS (SELECT 1 FROM "Subscribers" WHERE "HouseId" = OLD."Id")
              OR EXISTS (SELECT 1 FROM "Tickets" WHERE "HouseId" = OLD."Id")
              OR EXISTS (SELECT 1 FROM "HouseSchemes" WHERE "HouseId" = OLD."Id")
              OR EXISTS (SELECT 1 FROM "PprEntries" WHERE "HouseId" = OLD."Id")
              OR EXISTS (SELECT 1 FROM "IntercomPanels" WHERE "HouseId" = OLD."Id")
              OR EXISTS (SELECT 1 FROM "IntercomTickets" WHERE "HouseId" = OLD."Id")
            BEGIN
                SELECT RAISE(ABORT, 'AVERS_DELETE_LINKED_DATA');
            END;
            CREATE TRIGGER IF NOT EXISTS "avers_street_delete_guard"
            BEFORE DELETE ON "Streets"
            WHEN EXISTS (SELECT 1 FROM "Houses" WHERE "StreetId" = OLD."Id")
            BEGIN
                SELECT RAISE(ABORT, 'AVERS_DELETE_LINKED_DATA');
            END;
            """);
        transaction.Commit();
    }
}
