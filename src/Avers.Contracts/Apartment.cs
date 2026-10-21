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

/// <summary>Новая квартира в доме: абонент, договор и тариф, по которому начисляются месяцы текущего года.</summary>
public sealed record NewApartmentRequest(
    int HouseId,
    string? Section,
    string? Apartment,
    string? Account,
    string? LastName,
    string? FirstName,
    string? Patronymic,
    string? Phone,
    string? ContractNumber,
    DateOnly? ContractDate,
    decimal Tariff);

/// <summary>Через сколько месяцев без оплаты карточка подсвечивается жёлтым.</summary>
public sealed record PaymentWarningSettings(int Months);

/// <summary>Новый тариф с месяца: без дома и абонента — всем, иначе дому или одной квартире.</summary>
public sealed record TariffChangeRequest(int Year, int Month, decimal Tariff, int? HouseId, int? SubscriberId);

/// <summary>Сколько неоплаченных месяцев получили новый тариф.</summary>
public sealed record TariffChangeResult(int Months);

/// <summary>Сколько абонентов получили начисления на открытый год.</summary>
public sealed record OpenYearResult(int Subscribers);

/// <summary>Паспорт дома: техника СКПТ и примечания. Запрос заменяет все поля целиком.</summary>
public sealed record HousePassportRequest(
    string? Antennas,
    string? AerialCables,
    string? Amplifiers,
    string? PowerSupplies,
    string? TechFloorKey,
    string? OtherNotes);

/// <summary>Новая схема СКПТ дома: название и картинка JPG или PNG.</summary>
public sealed record AddSchemeRequest(string Name, byte[] Content);

/// <summary>Новая картинка для существующей схемы.</summary>
public sealed record SchemeFileRequest(byte[] Content);

/// <summary>Новое название схемы.</summary>
public sealed record RenameSchemeRequest(string Name);
