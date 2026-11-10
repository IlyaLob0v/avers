namespace Avers.Domain;

public sealed class Subscriber
{
    public int Id { get; set; }
    public int HouseId { get; set; }
    public House House { get; set; } = null!;
    public string InnerCode { get; set; } = "";
    public string Account { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string Patronymic { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Apartment { get; set; } = "";
    public string Section { get; set; } = "";
    public DateOnly? ContractDate { get; set; }
    public DateOnly? ContractTerminated { get; set; }
    public string ContractNumber { get; set; } = "";

    /// <summary>С кем заключён договор, если не с самим абонентом (ФИО полностью).</summary>
    public string ContractHolder { get; set; } = "";
    public string Notes { get; set; } = "";
    public string PzhreuIndex { get; set; } = "";
    public string PzhreuAccount { get; set; } = "";

    /// <summary>Лицевой счёт в едином платёжном документе ОЭК (11 цифр); заполняется загрузкой файла ОЭК.</summary>
    public string OekAccount { get; set; } = "";
    public int SettlementFrom { get; set; }
    public DateOnly? LastPaymentDate { get; set; }
    public bool PrintAccountWithPayDate { get; set; } = true;
    public const string NoBenefit = "не применяется";

    /// <summary>
    /// Льготы, которые ведёт АДС (22.10.2026); остальные оформляет МФЦ. Пока только пометка, на начисления не влияет.
    /// Прежняя льгота «юридическое лицо» стала типом клиента (<see cref="IsLegalEntity"/>).
    /// </summary>
    public static IReadOnlyList<string> Benefits { get; } = [NoBenefit, "коллективная оплата", "коллективный усилитель в квартире"];

    public string Benefit { get; set; } = NoBenefit;

    /// <summary>
    /// Клиент — юридическое лицо (контрагент). Название организации записано вместо фамилии, как в старом Аверсе.
    /// Реквизиты ниже заполняются только у юрлица.
    /// </summary>
    public bool IsLegalEntity { get; set; }
    public string Inn { get; set; } = "";
    public string Kpp { get; set; } = "";
    public string Ogrn { get; set; } = "";
    public string LegalAddress { get; set; } = "";

    /// <summary>Руководитель юрлица: должность и ФИО.</summary>
    public string Director { get; set; } = "";

    public string ClientKind => IsLegalEntity ? "Юрлицо" : "Физлицо";

    /// <summary>
    /// Что не так с реквизитами юрлица; null — всё верно. ИНН — 10 цифр (12 у ИП), КПП — 9, ОГРН — 13 (15 у ИП).
    /// Пустое поле допустимо: реквизиты вносят по мере получения.
    /// </summary>
    public static string? RequisitesError(string inn, string kpp, string ogrn) =>
        !Digits(inn, 10, 12) ? "ИНН — 10 цифр (у ИП — 12)."
        : !Digits(kpp, 9) ? "КПП — 9 цифр."
        : !Digits(ogrn, 13, 15) ? "ОГРН — 13 цифр (у ИП — 15)."
        : null;

    private static bool Digits(string value, params int[] lengths)
    {
        var v = value.Trim();
        return v.Length == 0 || (lengths.Contains(v.Length) && v.All(char.IsAsciiDigit));
    }

    public ICollection<YearPayment> Payments { get; set; } = new List<YearPayment>();
    public ICollection<WorkTicket> Tickets { get; set; } = new List<WorkTicket>();
    public ICollection<CourtCase> CourtCases { get; set; } = new List<CourtCase>();
    public ICollection<UnifiedReceipt> Receipts { get; set; } = new List<UnifiedReceipt>();

    /// <summary>С кем заключён договор: указанное лицо или сам абонент.</summary>
    public string ContractParty => string.IsNullOrWhiteSpace(ContractHolder) ? FullName : ContractHolder.Trim();

    public string FullName => string.Join(" ", new[] { LastName, FirstName, Patronymic }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string ShortName
    {
        get
        {
            var f = string.IsNullOrEmpty(FirstName) ? "" : $"{FirstName[0]}.";
            var p = string.IsNullOrEmpty(Patronymic) ? "" : $"{Patronymic[0]}.";
            return $"{LastName} {f}{p}".Trim();
        }
    }
}
