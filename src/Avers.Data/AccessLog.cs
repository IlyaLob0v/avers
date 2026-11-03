using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Журнал просмотров персональных данных абонентов (152-ФЗ, NFR-07).</summary>
public static class AccessLog
{
    public static List<AccessLogEntry> Latest(AversDbContext db) =>
        db.AccessLog.AsNoTracking().OrderByDescending(e => e.Id).Take(ChangeLog.Shown).ToList();

    public static void Add(AversDbContext db, DateTimeOffset at, string who, string action, string target)
    {
        db.AccessLog.Add(new AccessLogEntry { At = at, UserName = who, Action = action, Target = target.Length > 400 ? target[..400] : target });
        db.SaveChanges();
    }
}
