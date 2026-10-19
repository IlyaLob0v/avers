namespace Avers.Domain;

public static class Billing
{
    public const decimal DebtHighlight = 450m;
    public const int OldPaymentYears = 3;
    public const int ClaimUnpaidMonths = 3;

    /// <summary>Месяцы по текущий включительно: начисления на весь год заводятся заранее, будущие месяцы ещё не долг.</summary>
    public static List<YearPayment> Due(IEnumerable<YearPayment> payments, DateOnly today) =>
        payments.Where(p => p.Year * 12 + p.Month <= today.Year * 12 + today.Month).ToList();

    public static decimal Charged(IEnumerable<YearPayment> payments) =>
        payments.Sum(p => p.Tariff);

    public static decimal Paid(IEnumerable<YearPayment> payments) =>
        payments.Sum(p => p.Amount ?? 0);

    public static decimal Debt(IEnumerable<YearPayment> payments) =>
        Math.Max(0, Charged(payments) - Paid(payments));

    public static int UnpaidMonths(IEnumerable<YearPayment> payments) =>
        payments.Count(p => p.Amount is null);

    public static bool IsOldPayer(DateOnly? lastPayment, DateOnly today) =>
        lastPayment is null || lastPayment.Value <= today.AddYears(-OldPaymentYears);

    public static string KindTitle(TicketKind kind) => kind switch
    {
        TicketKind.Antenna => "Антенна / СКПТ",
        TicketKind.Cable => "Кабель / отвод",
        TicketKind.Amplifier => "Усилитель",
        TicketKind.Intercom => "Домофон",
        TicketKind.Provider => "Провайдер",
        _ => "Прочее"
    };

    public static string StatusTitle(TicketStatus status) => status switch
    {
        TicketStatus.Accepted => "",
        TicketStatus.InProgress => "",
        TicketStatus.Done => "да",
        TicketStatus.Deferred => "отл.",
        _ => ""
    };

    public static string PprTitle(PprStatus status) => status switch
    {
        PprStatus.Planned => "план",
        PprStatus.Done => "вып.",
        PprStatus.Overdue => "проср.",
        _ => ""
    };
}
