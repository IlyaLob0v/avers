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

/// <summary>Судебное дело должника. Запрос заменяет все поля целиком.</summary>
public sealed record CourtCaseRequest(string? DelaAversId, string? OrderNumber, DateOnly OrderDate, string? Court, decimal Amount, string? Status);

/// <summary>Через сколько месяцев без оплаты карточка подсвечивается жёлтым.</summary>
public sealed record PaymentWarningSettings(int Months);

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
