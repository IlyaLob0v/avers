namespace Avers.Contracts;

/// <summary>
/// Правка договора и примечания в карточке. Пустой <c>ContractHolder</c> — договор с самим абонентом. Запрос заменяет все поля целиком.
/// </summary>
public sealed record UpdateContractRequest(
    string? ContractNumber,
    DateOnly? ContractDate,
    DateOnly? ContractTerminated,
    string? ContractHolder,
    string? Notes);

/// <summary>Через сколько месяцев без оплаты карточка подсвечивается жёлтым.</summary>
public sealed record PaymentWarningSettings(int Months);
