namespace Avers.Domain;

/// <summary>Настройка, общая для всех рабочих мест (хранится в базе).</summary>
public sealed class AppSetting
{
    public const string PaymentWarningMonths = "payment.warning-months";
    public const string PaymentConfirmFrom = "payment.confirm-from";

    /// <summary>С какой суммы оплату надо подтвердить повторным нажатием.</summary>
    public const decimal DefaultPaymentConfirmFrom = 1000m;

    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
