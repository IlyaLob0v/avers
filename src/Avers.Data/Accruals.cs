using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Начисления: строки месяцев с тарифом, по которым вносят оплату (<see cref="Domain.YearPayment"/>).
/// </summary>
public static class Accruals
{
    /// <summary>
    /// Открывает год: 12 месяцев каждому абоненту, у которого договор не расторгнут,
    /// в году ещё нет ни одного месяца, а раньше были начисления. Тариф — последнее начисление абонента.
    /// Возвращает число абонентов.
    /// </summary>
    public static int OpenYear(AversDbContext db, int year, string who, DateTimeOffset at)
    {
        using var transaction = db.Database.BeginTransaction();
        var rows = db.Database.ExecuteSql($"""
            INSERT INTO "Payments" ("SubscriberId", "Year", "Month", "Tariff")
            SELECT s."Id", {year}, m."Month",
                   (SELECT p."Tariff" FROM "Payments" p WHERE p."SubscriberId" = s."Id" AND p."Year" < {year}
                    ORDER BY p."Year" DESC, p."Month" DESC LIMIT 1)
            FROM "Subscribers" s
            CROSS JOIN (SELECT 1 AS "Month" UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
                        UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8
                        UNION ALL SELECT 9 UNION ALL SELECT 10 UNION ALL SELECT 11 UNION ALL SELECT 12) m
            WHERE s."ContractTerminated" IS NULL
              AND NOT EXISTS (SELECT 1 FROM "Payments" p WHERE p."SubscriberId" = s."Id" AND p."Year" = {year})
              AND EXISTS (SELECT 1 FROM "Payments" p WHERE p."SubscriberId" = s."Id" AND p."Year" < {year})
            """);
        var opened = rows / 12;
        ChangeLog.Add(db, at, who, $"Начисления на {year} год", "все абоненты", opened > 0 ? [$"Открыто абонентов: {opened}"] : []);
        db.SaveChanges();
        transaction.Commit();
        return opened;
    }
}
