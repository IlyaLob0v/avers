namespace Avers.Contracts;

public sealed record StreetDto(int Id, string Name);

public sealed record HouseDto(int Id, int StreetId, string Number, string Address);

/// <summary>Строка списка найденных абонентов.</summary>
public sealed record SubscriberListItem(
    int Id,
    string Account,
    string InnerCode,
    string FullName,
    int HouseId,
    string Address,
    string Apartment,
    decimal Debt);

/// <summary>
/// Карточка квартиры: абонент, договор (номер, с кем заключён), обслуживающая дом организация
/// (в старом Аверсе — «домовладелец»), итоги по лицевому счёту,
/// последняя оплата и сколько месяцев прошло. <c>IsPaymentLate</c> — подсвечивать жёлтым
/// (порог <c>PaymentWarningMonths</c> из настроек).
/// </summary>
public sealed record SubscriberCard(
    int Id,
    string Account,
    string InnerCode,
    string LastName,
    string FirstName,
    string Patronymic,
    string FullName,
    string Phone,
    int HouseId,
    string Address,
    string Apartment,
    string Section,
    DateOnly? ContractDate,
    DateOnly? ContractTerminated,
    string Notes,
    string PzhreuIndex,
    string PzhreuAccount,
    string Benefit,
    DateOnly? LastPaymentDate,
    decimal Charged,
    decimal Paid,
    decimal Debt,
    int UnpaidMonths,
    bool IsDebtor,
    bool IsOldPayer,
    string ContractNumber,
    string ContractParty,
    string ServiceOrganization,
    PaymentDto? LastPayment,
    int? MonthsSinceLastPayment,
    bool IsPaymentLate,
    int PaymentWarningMonths);

/// <summary>Месяц лицевого счёта: начислено по тарифу и сколько внесено.</summary>
public sealed record PaymentDto(
    int Id,
    int Year,
    int Month,
    decimal Tariff,
    decimal? Amount,
    DateOnly? PaidAt,
    string? Source,
    bool IsPaid,
    string? RecordedBy = null,
    string? PayerName = null);

/// <summary>
/// Внести оплату. Всё, кроме года, можно не заполнять: месяц — первый неоплаченный,
/// сумма — тариф месяца, дата — сегодня, источник — «Касса». <c>PayerName</c> — кто оплатил, если не сам абонент.
/// Кто внёс оплату в программу, сервер записывает сам по вошедшему пользователю.
/// </summary>
public sealed record RecordPaymentRequest(
    int Year,
    int? Month = null,
    decimal? Amount = null,
    DateOnly? PaidAt = null,
    string? Source = null,
    string? PayerName = null);
