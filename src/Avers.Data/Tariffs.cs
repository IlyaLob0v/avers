using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Смена тарифа с месяца: для всех абонентов, одного дома или одной квартиры.</summary>
public static class Tariffs
{
    /// <summary>
    /// Ставит новый тариф неоплаченным месяцам начиная с <paramref name="year"/>.<paramref name="month"/>.
    /// Оплаченные месяцы не меняются. Без дома и квартиры — всем абонентам. Возвращает текст ошибки
    /// или null и число изменённых месяцев.
    /// </summary>
    public static (string? Error, int Months) Change(AversDbContext db, int year, int month, decimal tariff,
        int? houseId, int? subscriberId, string who, DateTimeOffset at)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return ("Месяц — в виде мм.гггг.", 0);
        if (tariff <= 0 || decimal.Round(tariff, 2) != tariff)
            return ("Тариф — больше нуля, не больше двух знаков после запятой.", 0);

        var months = db.Payments.Where(p => p.Amount == null && (p.Year > year || p.Year == year && p.Month >= month));
        string target;
        if (subscriberId is { } sid)
        {
            var s = db.Subscribers.FirstOrDefault(x => x.Id == sid);
            if (s is null)
                return ("Абонент не найден.", 0);
            months = months.Where(p => p.SubscriberId == sid);
            target = ChangeLog.Account(s);
        }
        else if (houseId is { } hid)
        {
            var house = db.Houses.Include(h => h.Street).FirstOrDefault(h => h.Id == hid);
            if (house is null)
                return ("Дом не найден.", 0);
            months = months.Where(p => p.Subscriber.HouseId == hid);
            target = house.AddressLine;
        }
        else
        {
            target = "все абоненты";
        }

        using var transaction = db.Database.BeginTransaction();
        var changed = months.ExecuteUpdate(u => u.SetProperty(p => p.Tariff, tariff));
        ChangeLog.Add(db, at, who, $"Тариф с {month:00}.{year}", target,
            changed > 0 ? [$"Тариф: {tariff.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))}", $"Месяцев: {changed}"] : []);
        db.SaveChanges();
        transaction.Commit();
        return (null, changed);
    }
}
