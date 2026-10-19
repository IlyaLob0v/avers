namespace Avers.Contracts;

/// <summary>Дом для радиомеханика с телефона (REQ-MECH-01): домовладелец, техника, открытая неисправность, схемы.</summary>
public sealed record MobileHouseDto(
    int Id,
    string Address,
    int Floors,
    int Entrances,
    int Apartments,
    string Homeowner,
    string HomeownerBranch,
    string HomeownerAddress,
    string HomeownerPhones,
    string HeadName,
    string HeadPhone,
    string EngineerName,
    string EngineerPhone,
    string Antennas,
    string AerialCables,
    string Amplifiers,
    string PowerSupplies,
    string TechFloorKey,
    string OtherNotes,
    string? OpenFault,
    string? OpenFaultReason,
    DateOnly? OpenFaultPlannedFix,
    List<MobileSchemeDto> Schemes);

/// <summary>Схема дома; картинку отдаёт <c>/api/mobile/schemes/{Id}</c>, если <see cref="HasImage"/>.</summary>
public sealed record MobileSchemeDto(int Id, string Name, string Format, DateOnly UpdatedAt, bool HasImage);
