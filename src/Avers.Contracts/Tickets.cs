namespace Avers.Contracts;

/// <summary>Строка журнала заявок АДС и сама заявка.</summary>
public sealed record TicketDto(
    int Id,
    int Number,
    DateOnly Date,
    TimeOnly Time,
    int HouseId,
    string Address,
    string Zone,
    string Homeowner,
    string Section,
    string Apartment,
    int? SubscriberId,
    string SubscriberName,
    string Account,
    string Phone,
    string Kind,
    string KindTitle,
    string Fault,
    string WorkDone,
    string Mechanic,
    string Status,
    string AcceptedBy,
    DateOnly? DoneDate,
    string RisersNote,
    bool CanEdit);

/// <summary>Принять заявку. Квартира и телефон берутся из лицевого счёта, если указан абонент.</summary>
public sealed record CreateTicketRequest(
    int HouseId,
    string Fault,
    string? Kind = null,
    int? SubscriberId = null,
    string? Section = null,
    string? Apartment = null,
    string? Phone = null,
    string? Mechanic = null,
    string? RisersNote = null);

/// <summary>Изменить заявку. Незаполненное поле не меняется; заполненное «что произведено» закрывает заявку.</summary>
public sealed record UpdateTicketRequest(
    string? Fault = null,
    string? WorkDone = null,
    string? Mechanic = null,
    string? Kind = null,
    string? Status = null,
    string? RisersNote = null);

/// <summary>Заявки за месяц года: принято, выполнено, осталось открытыми.</summary>
public sealed record TicketMonthDto(int Year, int Month, int Accepted, int Done, int Open);

/// <summary>Что антенная служба знает о доме: домовладелец, техника, открытая неисправность, ППР.</summary>
public sealed record HouseServiceInfoDto(
    int HouseId,
    string Address,
    string Zone,
    bool Serviced,
    int Floors,
    int Entrances,
    int Risers,
    int Apartments,
    string Homeowner,
    string HomeownerBranch,
    string HomeownerPhones,
    string Antennas,
    string AerialCables,
    string Amplifiers,
    string PowerSupplies,
    string TechFloorKey,
    string OtherNotes,
    DateOnly? LastPpr,
    string? OpenFault,
    string? OpenFaultReason,
    DateOnly? OpenFaultPlannedFix,
    int OpenTickets);
