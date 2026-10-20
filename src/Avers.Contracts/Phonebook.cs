namespace Avers.Contracts;

/// <summary>Улица в словаре.</summary>
public sealed record StreetRequest(string? Name);

/// <summary>Дом в словаре: адрес, данные ПЖРЭУ и размеры. Запрос заменяет все поля целиком.</summary>
public sealed record HouseRequest(
    int StreetId,
    string? Number,
    string? PzhreuIndex,
    string? PzhreuAccount,
    int Floors,
    int Entrances,
    int Risers,
    int Apartments,
    string? Zone,
    int? OrganizationId,
    bool Serviced);

/// <summary>Материал в словаре.</summary>
public sealed record MaterialRequest(string? Name, string? Unit);

/// <summary>Радиомеханик в словаре.</summary>
public sealed record MechanicRequest(string? FullName, string? Position, string? Phone, string? Address);
