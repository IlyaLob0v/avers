namespace Avers.Domain;

/// <summary>Настройка, общая для всех рабочих мест (хранится в базе).</summary>
public sealed class AppSetting
{
    public const string PaymentWarningMonths = "payment.warning-months";

    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
