using System.Security.Cryptography;
using System.Text;
using Avers.Data;
using Microsoft.EntityFrameworkCore;

namespace Avers.Desktop;

/// <summary>
/// Учебная база оболочки (avers.db) создаётся через EnsureCreated, миграций у неё нет.
/// Если модель данных поменялась (новые поля), старый файл не открыть — пересоздаём учебную базу.
/// Отпечаток схемы лежит рядом с базой в avers.db.schema.
/// </summary>
public static class DemoDatabase
{
    public static void EnsureCurrent(AversDbContext db, string databaseFile)
    {
        var schema = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(db.Database.GenerateCreateScript())));
        var marker = databaseFile + ".schema";
        var known = File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;

        if (File.Exists(databaseFile) && known != schema)
            db.Database.EnsureDeleted();

        db.Database.EnsureCreated();
        File.WriteAllText(marker, schema);
    }
}
