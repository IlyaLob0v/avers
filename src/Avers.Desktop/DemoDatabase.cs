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
        var schema = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(db.Database.GenerateCreateScript())));
        var marker = databaseFile + ".schema";
        var known = File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;

        if (File.Exists(databaseFile) && known != schema)
            db.Database.EnsureDeleted();

        db.Database.EnsureCreated();
        File.WriteAllText(marker, schema);
    }
}
