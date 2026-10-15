namespace Avers.Domain;

/// <summary>Сколько прошло с последней оплаты и когда подсвечивать абонента жёлтым.</summary>
public static class PaymentAge
{
    /// <summary>Порог по умолчанию: жёлтым — если с последней оплаты прошло 2 полных месяца и больше.</summary>
    public const int DefaultWarningMonths = 2;

    public const int MinWarningMonths = 1;
    public const int MaxWarningMonths = 36;

    /// <summary>
    /// Полных календарных месяцев от даты оплаты до сегодня: 09.07 → 31.08 = 1, 09.07 → 09.09 = 2.
    /// Оплаты не было — <c>null</c>.
    /// </summary>
    public static int? MonthsSince(DateOnly? lastPayment, DateOnly today)
    {
        if (lastPayment is not { } last)
            return null;
        if (last >= today)
            return 0;
        var months = (today.Year - last.Year) * 12 + today.Month - last.Month;
        if (last.AddMonths(months) > today)
            months--;
        return months;
    }

    /// <summary>Подсвечивать ли жёлтым. Оплаты не было вовсе — тоже подсвечиваем.</summary>
    public static bool IsLate(DateOnly? lastPayment, DateOnly today, int warningMonths = DefaultWarningMonths) =>
        MonthsSince(lastPayment, today) is not { } months || months >= warningMonths;

    /// <summary>
    /// Подсвечивать ли абонента. Оплат не было — срок считаем от даты договора: новый договор не горит,
    /// договор двухмесячной давности без единой оплаты — горит. Нет ни оплат, ни даты договора — горит.
    /// </summary>
    public static bool IsLate(Subscriber s, DateOnly today, int warningMonths = DefaultWarningMonths) =>
        IsLate(s.LastPaymentDate ?? s.ContractDate, today, warningMonths);

    public static bool IsValidThreshold(int months) => months is >= MinWarningMonths and <= MaxWarningMonths;

    /// <summary>«1 месяц», «2 месяца», «5 месяцев» — для подписи в карточке.</summary>
    public static string MonthsText(int months)
    {
        var n = months % 100;
        var word = n is >= 11 and <= 14 ? "месяцев" : (n % 10) switch
        {
            1 => "месяц",
            2 or 3 or 4 => "месяца",
            _ => "месяцев"
        };
        return $"{months} {word}";
    }

    /// <summary>Подпись для карточки: «1 месяц назад», «меньше месяца назад», «оплат не было».</summary>
    public static string AgoText(DateOnly? lastPayment, DateOnly today) => MonthsSince(lastPayment, today) switch
    {
        null => "оплат не было",
        0 => "меньше месяца назад",
        var m => $"{MonthsText(m.Value)} назад"
    };

    /// <summary>Последний внесённый платёж: самая поздняя дата оплаты, при равенстве — более поздний месяц.</summary>
    public static YearPayment? LastPayment(IEnumerable<YearPayment> payments) =>
        payments.Where(p => p.Amount is not null && p.PaidAt is not null)
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .FirstOrDefault();
}
