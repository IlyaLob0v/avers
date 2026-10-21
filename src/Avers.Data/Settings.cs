using System.Globalization;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Чтение и запись общих настроек (таблица Settings).</summary>
public static class Settings
{
    /// <summary>Через сколько месяцев без оплаты карточка подсвечивается жёлтым.</summary>
    public static int PaymentWarningMonths(AversDbContext db)
    {
        var raw = db.Settings.AsNoTracking().Where(x => x.Key == AppSetting.PaymentWarningMonths)
            .Select(x => x.Value).FirstOrDefault();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var months)
               && PaymentAge.IsValidThreshold(months)
            ? months
            : PaymentAge.DefaultWarningMonths;
    }

    /// <summary>Меняет порог для всех рабочих мест; правка попадает в журнал изменений.</summary>
    public static void SetPaymentWarningMonths(AversDbContext db, int months, string who, DateTimeOffset at)
    {
        if (!PaymentAge.IsValidThreshold(months))
            throw new ArgumentOutOfRangeException(nameof(months), months,
                $"Порог — от {PaymentAge.MinWarningMonths} до {PaymentAge.MaxWarningMonths} месяцев.");

        ChangeLog.Add(db, at, who, "Настройки", "все рабочие места",
            ChangeLog.Diff(("Жёлтая подсветка, мес. без оплаты", PaymentWarningMonths(db), months)));
        var value = months.ToString(CultureInfo.InvariantCulture);
        var row = db.Settings.FirstOrDefault(x => x.Key == AppSetting.PaymentWarningMonths);
        if (row is null)
            db.Settings.Add(new AppSetting { Key = AppSetting.PaymentWarningMonths, Value = value });
        else
            row.Value = value;
        db.SaveChanges();
    }
}
