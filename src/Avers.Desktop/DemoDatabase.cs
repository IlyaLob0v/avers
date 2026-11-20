using System.Security.Cryptography;
using System.Text;
using Avers.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Avers.Desktop;

/// <summary>
/// Учебная база оболочки (avers.db) создаётся через EnsureCreated, миграций у неё нет.
/// Если модель данных или учебные данные (<see cref="DemoSeeder.Revision"/>) поменялись, пересоздаём учебную базу.
/// Отпечаток схемы лежит рядом с базой в avers.db.schema.
/// </summary>
public static class DemoDatabase
{
    /// <summary>
    /// Пока один экземпляр программы готовит базу, второй (двойной щелчок по ярлыку, второй сеанс
    /// того же сотрудника на терминальном сервере) ждёт: иначе один удалит файл, который открывает другой.
    /// </summary>
    public static IDisposable Lock(string databaseFile, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            try
            {
                return new FileStream(databaseFile + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
            }
        }
    }

    public static void EnsureCurrent(AversDbContext db, string databaseFile)
    {
        var schema = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(db.Database.GenerateCreateScript() + DemoSeeder.Revision)));
        var marker = databaseFile + ".schema";
        var known = File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;

        if (File.Exists(databaseFile) && known != schema)
        {
            var backup = databaseFile + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".db";
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databaseFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Pooling = false }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }
            db.Database.CloseConnection();
            db.Database.EnsureDeleted();
        }

        db.Database.EnsureCreated();
        File.WriteAllText(marker + ".tmp", schema);
        File.Move(marker + ".tmp", marker, overwrite: true);
    }
}
