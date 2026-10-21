namespace Avers.Data;

/// <summary>Отмена ошибочно внесённой оплаты: месяц снова не оплачен, прежние данные остаются в журнале изменений.</summary>
public static class PaymentCancellation
{
    /// <summary>Возвращает текст ошибки или null.</summary>
    public static string? Cancel(AversDbContext db, int subscriberId, int paymentId, string who, DateTimeOffset at)
    {
        var payment = db.Payments.FirstOrDefault(p => p.Id == paymentId && p.SubscriberId == subscriberId);
        if (payment is null)
            return "Месяц не найден.";
        if (payment.Amount is null)
            return $"Месяц {payment.Month:00}.{payment.Year} не оплачен.";

        var subscriber = db.Subscribers.First(s => s.Id == subscriberId);
        ChangeLog.Add(db, at, who, $"Отмена оплаты за {payment.Month:00}.{payment.Year}", ChangeLog.Account(subscriber), ChangeLog.Diff(
            ("Сумма", payment.Amount, null),
            ("Дата оплаты", payment.PaidAt, null),
            ("Источник", payment.Source, null),
            ("Внёс", payment.RecordedBy, null),
            ("Оплатил", payment.PayerName, null)));
        payment.Amount = null;
        payment.PaidAt = null;
        payment.Source = null;
        payment.RecordedBy = null;
        payment.PayerName = null;
        subscriber.LastPaymentDate = db.Payments
            .Where(p => p.SubscriberId == subscriberId && p.Id != paymentId && p.PaidAt != null)
            .Max(p => p.PaidAt);
        db.SaveChanges();
        return null;
    }
}
