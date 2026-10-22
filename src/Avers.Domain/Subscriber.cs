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
    public string Benefit { get; set; } = "не применяется";

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
