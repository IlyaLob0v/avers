using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Внесение оплаты, как кнопка «Внести оплату»: одно правило для сервера и учебной базы.</summary>
public static class Payments
{
    public const string DefaultSource = "Касса";

    /// <summary>
    /// Оплата месяца <paramref name="month"/>, а без него — первого неоплаченного месяца года; сумма null — по тарифу.
    /// Оплаченным месяц считается, когда сумма заполнена, в том числе 0 ₽ — так в текущем коде
    /// (вопрос к бухгалтерии открыт, docs/07-open-questions.md). Возвращает месяц или текст ошибки.
    /// </summary>
    public static (YearPayment? Payment, string? Error) Record(AversDbContext db, Subscriber subscriber, int year, int? month,
        decimal? amount, DateOnly paidAt, string? source, string? payerName, string recordedBy, string who, DateTimeOffset at,
        Action<YearPayment>? recordReceipt = null)
    {
        var months = db.Payments.Where(p => p.SubscriberId == subscriber.Id && p.Year == year);
        var target = month is { } m
            ? months.FirstOrDefault(p => p.Month == m)
            : months.Where(p => p.Amount == null).OrderBy(p => p.Month).FirstOrDefault();
        if (target is null)
            return (null, month is null ? $"Все месяцы {year} уже оплачены." : $"За {month:00}.{year} у абонента нет начисления.");
        if (target.Amount is not null)
            return (null, $"Месяц {target.Month:00}.{target.Year} уже оплачен.");

        var paid = amount ?? target.Tariff;
        source = string.IsNullOrWhiteSpace(source) ? DefaultSource : source.Trim();
        payerName = string.IsNullOrWhiteSpace(payerName) ? null : payerName.Trim();

        // Условие Amount == null в самом UPDATE: если два кассира внесут один месяц одновременно,
        // пройдёт только первый.
        using var transaction = db.Database.BeginTransaction();
        var updated = db.Payments
            .Where(p => p.Id == target.Id && p.Amount == null)
            .ExecuteUpdate(u => u
                .SetProperty(p => p.Amount, paid)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.ImportedFromJack, false)
                .SetProperty(p => p.Source, source)
                .SetProperty(p => p.RecordedBy, recordedBy)
                .SetProperty(p => p.PayerName, payerName));
        if (updated == 0)
            return (null, $"Месяц {target.Month:00}.{target.Year} уже оплачен.");

        db.Subscribers.Where(s => s.Id == subscriber.Id && (s.LastPaymentDate == null || s.LastPaymentDate < paidAt))
            .ExecuteUpdate(u => u.SetProperty(s => s.LastPaymentDate, paidAt));
        ChangeLog.Add(db, at, who, $"Оплата за {target.Month:00}.{target.Year}", ChangeLog.Account(subscriber), ChangeLog.Diff(
            ("Сумма", target.Amount, paid),
            ("Дата оплаты", target.PaidAt, paidAt),
            ("Источник", target.Source, source),
            ("Плательщик", target.PayerName, payerName)));
        // ExecuteUpdate идёт мимо отслеживаемых объектов. Чек строим из записанных значений.
        db.Entry(target).Reload();
        db.Entry(subscriber).Reload();
        recordReceipt?.Invoke(target);
        db.SaveChanges();
        transaction.Commit();
        return (target, null);
    }
}
