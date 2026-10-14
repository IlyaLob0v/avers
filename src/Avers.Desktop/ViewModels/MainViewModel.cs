using System.Collections.ObjectModel;
using System.Globalization;
using Avers.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace Avers.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDbContextFactory<AversDbContext> _db;
    private readonly DateOnly _today = DemoSeeder.WorkDate;
    private List<House> _houses = [];
    private List<Subscriber> _subscribers = [];
    private List<WorkTicket> _tickets = [];

    public MainViewModel(IDbContextFactory<AversDbContext> db) => _db = db;

    public ObservableCollection<AppUser> Users { get; } = [];
    public ObservableCollection<Street> Streets { get; } = [];
    public ObservableCollection<House> HousesOnStreet { get; } = [];
    public ObservableCollection<WorkTicket> Tickets { get; } = [];
    public ObservableCollection<WorkTicket> HouseTickets { get; } = [];
    public ObservableCollection<WorkTicket> SubscriberTickets { get; } = [];
    public ObservableCollection<Mechanic> Mechanics { get; } = [];
    public ObservableCollection<Material> Materials { get; } = [];
    public ObservableCollection<UnifiedReceipt> Receipts { get; } = [];
    public ObservableCollection<Subscriber> Contracts { get; } = [];
    public ObservableCollection<DebtorRow> Debtors { get; } = [];
    public ObservableCollection<PprRow> PprRows { get; } = [];
    public ObservableCollection<AccessRow> AccessRows { get; } = [];
    public ObservableCollection<ReportMetric> ReportMetrics { get; } = [];
    public ObservableCollection<HouseScheme> Schemes { get; } = [];
    public ObservableCollection<IntercomTicket> IntercomTickets { get; } = [];
    public ObservableCollection<PaymentGridRow> PaymentRows { get; } = [];
    public IReadOnlyList<(UserRole Role, string Title)> Roles { get; } = RoleCatalog.All;

    [ObservableProperty] private AppUser? _loginUser;
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string? _loginError;
    [ObservableProperty] private AppUser? _user;
    [ObservableProperty] private AppModule _module = AppModule.Desktop;
    [ObservableProperty] private Street? _selectedStreet;
    [ObservableProperty] private House? _selectedHouse;
    [ObservableProperty] private Subscriber? _selectedSubscriber;
    [ObservableProperty] private WorkTicket? _selectedTicket;
    [ObservableProperty] private DebtorRow? _selectedDebtor;
    [ObservableProperty] private SearchMode _searchMode = SearchMode.Address;
    [ObservableProperty] private string _findStreet = "Волочаевская";
    [ObservableProperty] private string _findHouse = "19Д";
    [ObservableProperty] private string _findSection = "2";
    [ObservableProperty] private string _findApartment = "68";
    [ObservableProperty] private string _findQuery = "";
    [ObservableProperty] private string _status = "Готово.";
    [ObservableProperty] private bool _allTickets = true;
    [ObservableProperty] private bool _onlyActiveContracts = true;
    [ObservableProperty] private string _ticketFault = "";
    [ObservableProperty] private string _ticketWork = "";
    [ObservableProperty] private string _ticketMechanic = "";
    [ObservableProperty] private string _claimText = "";
    [ObservableProperty] private DateOnly _reportFrom = new(2026, 1, 1);
    [ObservableProperty] private DateOnly _reportTo = DemoSeeder.WorkDate;
    [ObservableProperty] private IntercomPanel? _intercom;

    public string LoginRoleTitle => LoginUser is null ? "" : RoleCatalog.Title(LoginUser.Role);
    public bool ShowLogin => User is null;
    public bool ShowShell => User is not null;
    public bool ShowDesktop => Module == AppModule.Desktop;
    public bool ShowAbon => Module == AppModule.Abon;
    public bool ShowDisp => Module == AppModule.Disp;
    public bool ShowTicket => Module == AppModule.Ticket;
    public bool ShowAnten => Module == AppModule.Anten;
    public bool ShowAntenInfo => Module == AppModule.AntenInfo;
    public bool ShowScheme => Module == AppModule.Scheme;
    public bool ShowSubscriber => Module == AppModule.Subscriber;
    public bool ShowPrDubl => Module == AppModule.PrDubl;
    public bool ShowContract => Module == AppModule.Contract;
    public bool ShowPrint => Module == AppModule.Print;
    public bool ShowPpr => Module == AppModule.Ppr;
    public bool ShowLegal => Module == AppModule.Legal;
    public bool ShowClaim => Module == AppModule.Claim;
    public bool ShowReports => Module == AppModule.Reports;
    public bool ShowPhonebook => Module == AppModule.Phonebook;
    public bool ShowDomofo => Module == AppModule.Domofo;
    public bool ShowReader => Module == AppModule.Reader;
    public bool ShowAccess => Module == AppModule.Access;
    public bool SearchByAddress => SearchMode == SearchMode.Address;
    public bool SearchByPzhreu => SearchMode == SearchMode.Pzhreu;
    public bool SearchByAccount => SearchMode == SearchMode.Account;
    public bool SearchByInner => SearchMode == SearchMode.InnerCode;
    public bool SearchBySurname => SearchMode == SearchMode.Surname;

    public string WindowTitle => Module switch
    {
        AppModule.Abon => $"Ввод платежей абонентов за {_today:dd.MM.yyyy}",
        AppModule.Disp => $"Приём заявок от населения · {_today:dd.MM.yyyy}",
        AppModule.Ticket => SelectedTicket is null ? "Заявка" : $"Заявка № {SelectedTicket.Number} от {SelectedTicket.Date:dd.MM.yyyy}",
        AppModule.Anten => SelectedHouse is null ? "Паспорт дома" : $"Паспорт дома · {SelectedHouse.AddressLine}",
        AppModule.AntenInfo => "Сведения по антенне / дому",
        AppModule.Scheme => SelectedHouse is null ? "Схема СКПТ" : $"Схема СКПТ · {SelectedHouse.AddressLine}",
        AppModule.Subscriber => SelectedSubscriber?.FullName ?? "Карточка абонента",
        AppModule.PrDubl => "Данные о платежах по единым квитанциям",
        AppModule.Contract => SelectedHouse is null ? "Договоры" : $"Договоры · {SelectedHouse.AddressLine}",
        AppModule.Print => "Квитанция на оплату ТО СКПТ",
        AppModule.Ppr => "Сервис → Графики ППР",
        AppModule.Legal => "Должники · юристы + Дело-Аверс",
        AppModule.Claim => "Досудебное уведомление",
        AppModule.Reports => "Отчёты · XLSX / PDF",
        AppModule.Phonebook => "Словарь",
        AppModule.Domofo => SelectedHouse is null ? "Домофон" : $"Домофон · {SelectedHouse.AddressLine}",
        AppModule.Reader => "Просмотр дома — без правки",
        AppModule.Access => "Права доступа",
        _ => "Аверс"
    };

    public string StatusLeft => Module switch
    {
        AppModule.Abon => "AversAbon · платежи",
        AppModule.Disp => "AversDisp",
        AppModule.Anten => "AversAnten",
        AppModule.Domofo => "domofo",
        AppModule.Phonebook => "phonebook",
        AppModule.PrDubl => "PrDublPay",
        AppModule.Contract => "aversContract",
        AppModule.AntenInfo => "anteninfo",
        _ => "Аверс"
    };

    public string StatusRight => User is null ? "" : $"{User.DisplayName} · {RoleCatalog.Title(User.Role)} · {_today:dd.MM.yyyy}";
    public string WorkDateText => _today.ToString("dd.MM.yyyy");
    public string FaultBanner => SelectedHouse is { OpenFault: not null } h
        ? $"Неустранённая неисправность СКПТ: {h.OpenFault}. Причина: {h.OpenFaultReason}. План устранения: {h.OpenFaultPlannedFix:dd.MM.yyyy}."
        : "";
    public bool HasFault => !string.IsNullOrWhiteSpace(FaultBanner);
    public string ChargedText => Money(SelectedSubscriber is null ? 0 : Billing.Charged(SelectedSubscriber.Payments));
    public string PaidText => Money(SelectedSubscriber is null ? 0 : Billing.Paid(SelectedSubscriber.Payments));
    public string DebtText => Money(SelectedSubscriber is null ? 0 : Billing.Debt(SelectedSubscriber.Payments));
    public string ToPayText => SelectedSubscriber is null ? "" : Money(95);
    public string OekWarning
    {
        get
        {
            var day = _tickets.Where(t => t.Date == _today).ToList();
            var accepted = day.Count;
            var done = day.Count(t => t.Status == TicketStatus.Done);
            return accepted > done
                ? $"На {_today:dd.MM.yyyy} принято {accepted}, выполнено {done}. Список для ОЭК скрыт."
                : $"На {_today:dd.MM.yyyy} принято {accepted}, выполнено {done}. Можно выгрузить список для ОЭК.";
        }
    }

    public string HouseCaption => SelectedHouse is null ? "" : SelectedHouse.AddressLine;
    public string HouseStats => SelectedHouse is null ? "" :
        $"{SelectedHouse.Floors} этажей · {SelectedHouse.Entrances} подъезда · {SelectedHouse.Risers} стояков · {SelectedHouse.Apartments} кв. · зона {SelectedHouse.Zone} · {(SelectedHouse.Serviced ? "на обслуживании" : "снят")} · индекс {SelectedHouse.PzhreuIndex} · счёт {SelectedHouse.PzhreuAccount}";

    public string HouseAptLine => SelectedSubscriber is null ? "" : $"{SelectedSubscriber.House.Number} / {SelectedSubscriber.Section} / {SelectedSubscriber.Apartment}";
    public string LastPayCell => SelectedSubscriber?.LastPaymentDate is { } d ? d.ToString("dd.MM") : "";
    public string ContractDateText => SelectedSubscriber?.ContractDate is { } d ? d.ToString("dd.MM.yyyy") : "";
    public string LastPprText => SelectedHouse?.LastPpr is { } d ? d.ToString("dd.MM.yyyy") : "";
    public IReadOnlyList<string> Months { get; } = ["Янв","Фев","Мар","Апр","Май","Июн","Июл","Авг","Сен","Окт","Ноя","Дек"];

    public void Load()
    {
        using var db = _db.CreateDbContext();
        Users.Clear();
        foreach (var u in db.Users.AsNoTracking().OrderBy(x => x.Login))
            Users.Add(u);
        LoginUser = Users.FirstOrDefault(x => x.Login == "sokolova") ?? Users.FirstOrDefault();

        Streets.Clear();
        foreach (var s in db.Streets.AsNoTracking().OrderBy(x => x.Name))
            Streets.Add(s);

        _houses = db.Houses.AsNoTracking().Include(h => h.Street).Include(h => h.Organization).Include(h => h.Schemes).Include(h => h.Ppr).Include(h => h.Intercoms).ToList();
        _subscribers = db.Subscribers.AsNoTracking().Include(s => s.House).ThenInclude(h => h.Street).Include(s => s.Payments).Include(s => s.CourtCases).Include(s => s.Receipts).ToList();
        _tickets = db.Tickets.AsNoTracking().Include(t => t.House).ThenInclude(h => h.Street).Include(t => t.Subscriber).ToList();

        Mechanics.Clear();
        foreach (var m in db.Mechanics.AsNoTracking().OrderBy(x => x.FullName)) Mechanics.Add(m);
        Materials.Clear();
        foreach (var m in db.Materials.AsNoTracking()) Materials.Add(m);

        AccessRows.Clear();
        foreach (var (role, title) in RoleCatalog.All)
            AccessRows.Add(AccessRow.From(role, title));

        SelectedStreet = Streets.FirstOrDefault(s => s.Name == "Волочаевская") ?? Streets.FirstOrDefault();
        RefreshDerived();
        SelectedHouse = HousesOnStreet.FirstOrDefault(h => h.Number == "19Д") ?? HousesOnStreet.FirstOrDefault();
        SelectedSubscriber = _subscribers.FirstOrDefault(s => s.Account == "1932068");
        SelectedTicket = _tickets.FirstOrDefault(t => t.Number == 1001);
        BindTicket();
        RefreshDerived();
    }

    partial void OnLoginUserChanged(AppUser? value) => OnPropertyChanged(nameof(LoginRoleTitle));

    partial void OnUserChanged(AppUser? value)
    {
        OnPropertyChanged(nameof(ShowLogin));
        OnPropertyChanged(nameof(ShowShell));
        OnPropertyChanged(nameof(StatusRight));
        NotifyModule();
    }

    partial void OnModuleChanged(AppModule value) => NotifyModule();

    partial void OnSelectedStreetChanged(Street? value)
    {
        RefreshHouses();
        SelectedHouse = HousesOnStreet.FirstOrDefault();
    }

    partial void OnSelectedHouseChanged(House? value)
    {
        if (value is not null)
        {
            var match = _subscribers.FirstOrDefault(s => s.HouseId == value.Id);
            if (match is not null && SelectedSubscriber?.HouseId != value.Id)
                SelectedSubscriber = match;
        }
        RefreshDerived();
    }

    partial void OnSelectedSubscriberChanged(Subscriber? value)
    {
        RebuildPayments();
        RefreshDerived();
    }

    partial void OnSelectedTicketChanged(WorkTicket? value) => BindTicket();
    partial void OnSelectedDebtorChanged(DebtorRow? value) => BuildClaim();
    partial void OnSearchModeChanged(SearchMode value)
    {
        OnPropertyChanged(nameof(SearchByAddress));
        OnPropertyChanged(nameof(SearchByPzhreu));
        OnPropertyChanged(nameof(SearchByAccount));
        OnPropertyChanged(nameof(SearchByInner));
        OnPropertyChanged(nameof(SearchBySurname));
    }
    partial void OnAllTicketsChanged(bool value) => RefreshTickets();
    partial void OnOnlyActiveContractsChanged(bool value) => RefreshDerived();

    private void NotifyModule()
    {
        OnPropertyChanged(nameof(ShowDesktop));
        OnPropertyChanged(nameof(ShowAbon));
        OnPropertyChanged(nameof(ShowDisp));
        OnPropertyChanged(nameof(ShowTicket));
        OnPropertyChanged(nameof(ShowAnten));
        OnPropertyChanged(nameof(ShowAntenInfo));
        OnPropertyChanged(nameof(ShowScheme));
        OnPropertyChanged(nameof(ShowSubscriber));
        OnPropertyChanged(nameof(ShowPrDubl));
        OnPropertyChanged(nameof(ShowContract));
        OnPropertyChanged(nameof(ShowPrint));
        OnPropertyChanged(nameof(ShowPpr));
        OnPropertyChanged(nameof(ShowLegal));
        OnPropertyChanged(nameof(ShowClaim));
        OnPropertyChanged(nameof(ShowReports));
        OnPropertyChanged(nameof(ShowPhonebook));
        OnPropertyChanged(nameof(ShowDomofo));
        OnPropertyChanged(nameof(ShowReader));
        OnPropertyChanged(nameof(ShowAccess));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(StatusLeft));
    }

    private void RefreshHouses()
    {
        HousesOnStreet.Clear();
        if (SelectedStreet is null) return;
        foreach (var h in _houses.Where(x => x.StreetId == SelectedStreet.Id).OrderBy(x => x.Number, StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), false)))
            HousesOnStreet.Add(h);
    }

    private void RefreshTickets()
    {
        Tickets.Clear();
        var q = _tickets.AsEnumerable();
        if (!AllTickets && SelectedHouse is not null)
            q = q.Where(t => t.HouseId == SelectedHouse.Id);
        foreach (var t in q.OrderByDescending(t => t.Date).ThenByDescending(t => t.Time))
            Tickets.Add(t);

        HouseTickets.Clear();
        if (SelectedHouse is not null)
            foreach (var t in _tickets.Where(t => t.HouseId == SelectedHouse.Id).OrderByDescending(t => t.Date))
                HouseTickets.Add(t);

        SubscriberTickets.Clear();
        if (SelectedSubscriber is not null)
            foreach (var t in _tickets.Where(t => t.SubscriberId == SelectedSubscriber.Id).OrderByDescending(t => t.Date))
                SubscriberTickets.Add(t);
    }

    private void RefreshDerived()
    {
        RefreshHouses();
        RefreshTickets();
        RebuildPayments();
        Receipts.Clear();
        if (SelectedSubscriber is not null)
            foreach (var r in SelectedSubscriber.Receipts.OrderByDescending(x => x.Moment)) Receipts.Add(r);

        Contracts.Clear();
        var list = _subscribers.AsEnumerable();
        if (SelectedHouse is not null) list = list.Where(s => s.HouseId == SelectedHouse.Id);
        if (OnlyActiveContracts) list = list.Where(s => s.ContractTerminated is null);
        foreach (var s in list.OrderBy(s => s.Section).ThenBy(s => s.Apartment)) Contracts.Add(s);

        Debtors.Clear();
        foreach (var s in _subscribers)
        {
            var debt = Billing.Debt(s.Payments);
            var unpaid = Billing.UnpaidMonths(s.Payments);
            var old = Billing.IsOldPayer(s.LastPaymentDate, _today);
            if (debt >= Billing.DebtHighlight || old)
                Debtors.Add(new DebtorRow(s, debt, unpaid, old));
        }
        SelectedDebtor ??= Debtors.FirstOrDefault();

        PprRows.Clear();
        foreach (var h in _houses.OrderBy(h => h.Street.Name).ThenBy(h => h.Number))
            PprRows.Add(new PprRow(h));

        Schemes.Clear();
        if (SelectedHouse is not null)
            foreach (var s in SelectedHouse.Schemes) Schemes.Add(s);

        Intercom = SelectedHouse?.Intercoms.FirstOrDefault();
        IntercomTickets.Clear();
        if (SelectedHouse is not null)
        {
            using var db = _db.CreateDbContext();
            foreach (var t in db.IntercomTickets.AsNoTracking().Where(x => x.HouseId == SelectedHouse.Id).OrderByDescending(x => x.Date))
                IntercomTickets.Add(t);
        }

        ReportMetrics.Clear();
        ReportMetrics.Add(new("Принято заявок", _tickets.Count(t => t.Date >= ReportFrom && t.Date <= ReportTo).ToString(), true));
        ReportMetrics.Add(new("Выполнено заявок", _tickets.Count(t => t.Status == TicketStatus.Done).ToString(), true));
        ReportMetrics.Add(new("Неустранённые неисправности СКПТ", _houses.Count(h => h.OpenFault != null).ToString(), true));
        ReportMetrics.Add(new("Сумма дебиторской задолженности", Money(_subscribers.Sum(s => Billing.Debt(s.Payments))), true));
        ReportMetrics.Add(new("Нагрузка на радиомехаников", "—", false));

        OnPropertyChanged(nameof(FaultBanner));
        OnPropertyChanged(nameof(HasFault));
        OnPropertyChanged(nameof(ChargedText));
        OnPropertyChanged(nameof(PaidText));
        OnPropertyChanged(nameof(DebtText));
        OnPropertyChanged(nameof(ToPayText));
        OnPropertyChanged(nameof(OekWarning));
        OnPropertyChanged(nameof(HouseCaption));
        OnPropertyChanged(nameof(HouseStats));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ContractDateText));
        OnPropertyChanged(nameof(LastPprText));
        OnPropertyChanged(nameof(Intercom));
        OnPropertyChanged(nameof(HouseAptLine));
        BuildClaim();
    }

    private void RebuildPayments()
    {
        PaymentRows.Clear();
        var pays = SelectedSubscriber?.Payments.Where(p => p.Year == 2026).OrderBy(p => p.Month).ToList() ?? [];
        PaymentRows.Add(new PaymentGridRow("Тариф", pays.Select(p => p.Tariff.ToString("N2")).ToArray()));
        PaymentRows.Add(new PaymentGridRow("Отв.", pays.Select(p => p.Source ?? "").ToArray()));
        PaymentRows.Add(new PaymentGridRow("Сумма", pays.Select(p => p.Amount is null ? "" : p.Amount.Value.ToString("N2")).ToArray()));
        PaymentRows.Add(new PaymentGridRow("Дата опл.", pays.Select(p => p.PaidAt is null ? "" : p.PaidAt.Value.ToString("dd.MM")).ToArray()));
    }

    private void BindTicket()
    {
        TicketFault = SelectedTicket?.Fault ?? "";
        TicketWork = SelectedTicket?.WorkDone ?? "";
        TicketMechanic = SelectedTicket?.Mechanic ?? "";
    }

    private void BuildClaim()
    {
        var row = SelectedDebtor ?? Debtors.FirstOrDefault(d => d.Unpaid >= 3);
        if (row is null) { ClaimText = ""; return; }
        ClaimText =
            $"Досудебное уведомление\n\n{row.FullName}, л/с {row.Subscriber.Account}, {row.Address}.\n\n" +
            $"Задолженность за ТО СКПТ свыше трёх месяцев — {row.DebtText}. Предлагаем погасить долг в течение 30 дней. По истечении срока материалы будут направлены в суд.\n\n" +
            $"{_today:dd.MM.yyyy}, антенная служба";
        OnPropertyChanged(nameof(ClaimText));
    }

    [RelayCommand]
    private void Login()
    {
        LoginError = null;
        if (LoginUser is null) { LoginError = "Выберите пользователя."; return; }
        if (!Avers.Data.Password.Verify(LoginUser, this.Password))
        {
            LoginError = "Неверный пароль. Для учебной базы пароль у всех: avers";
            return;
        }
        User = LoginUser;
        Password = "";
        Module = User.Role switch
        {
            UserRole.Dispatcher => AppModule.Desktop,
            UserRole.Lawyer => AppModule.Legal,
            UserRole.Reader => AppModule.Reader,
            UserRole.ChiefEngineer => AppModule.Anten,
            UserRole.AdsHead => AppModule.Abon,
            _ => AppModule.Desktop
        };
        Status = $"Вход: {User.DisplayName}, {RoleCatalog.Title(User.Role)}.";
    }

    [RelayCommand]
    private void Logout()
    {
        User = null;
        Module = AppModule.Desktop;
        Status = "Сеанс завершён.";
    }

    [RelayCommand]
    private void Go(AppModule page)
    {
        if (User is null) return;
        Module = page;
        var check = page switch
        {
            AppModule.Ticket => AppModule.Disp,
            AppModule.Subscriber or AppModule.Print or AppModule.PrDubl => AppModule.Abon,
            AppModule.Scheme or AppModule.AntenInfo => AppModule.Anten,
            AppModule.Claim => AppModule.Legal,
            _ => page
        };
        if (page is not AppModule.Desktop && !RoleCatalog.Can(User.Role, check, out _))
            Status = $"Макет окна открыт. Для роли «{RoleCatalog.Title(User.Role)}» это место в работе будет с ограниченными правами.";
        else
            Status = WindowTitle;
    }

    [RelayCommand]
    private void SetSearch(SearchMode mode) => SearchMode = mode;

    [RelayCommand]
    private void Find()
    {
        IEnumerable<Subscriber> q = _subscribers;
        q = SearchMode switch
        {
            SearchMode.Account => q.Where(s => s.Account.Contains(FindQuery.Trim(), StringComparison.OrdinalIgnoreCase)),
            SearchMode.InnerCode => q.Where(s => s.InnerCode.Contains(FindQuery.Trim(), StringComparison.OrdinalIgnoreCase)),
            SearchMode.Pzhreu => q.Where(s => s.PzhreuIndex.Contains(FindQuery.Trim(), StringComparison.OrdinalIgnoreCase) || s.PzhreuAccount.Contains(FindQuery.Trim(), StringComparison.OrdinalIgnoreCase)),
            SearchMode.Surname => q.Where(s => s.LastName.StartsWith(FindQuery.Trim(), StringComparison.OrdinalIgnoreCase) && FindQuery.Trim().Length >= 4),
            _ => q.Where(s =>
                s.House.Street.Name.StartsWith(FindStreet.Trim(), StringComparison.OrdinalIgnoreCase) &&
                s.House.Number.Equals(FindHouse.Trim(), StringComparison.OrdinalIgnoreCase) &&
                s.Apartment == FindApartment.Trim())
        };
        var found = q.FirstOrDefault();
        if (found is null) { Status = "Абонент не найден."; return; }
        SelectedStreet = Streets.First(s => s.Id == found.House.StreetId);
        SelectedHouse = HousesOnStreet.FirstOrDefault(h => h.Id == found.HouseId);
        SelectedSubscriber = found;
        Status = $"Найден {found.FullName}, кв. {found.Apartment}.";
    }

    [RelayCommand]
    private void RecordPayment()
    {
        if (SelectedSubscriber is null) return;
        if (User is { CanEditAccount: false, Role: not UserRole.AdsHead and not UserRole.Administrator })
        {
            Status = "Нет права вносить оплату.";
            return;
        }
        using var db = _db.CreateDbContext();
        var next = db.Payments.Where(p => p.SubscriberId == SelectedSubscriber.Id && p.Year == 2026 && p.Amount == null)
            .OrderBy(p => p.Month).FirstOrDefault();
        if (next is null) { Status = "Все месяцы 2026 уже оплачены."; return; }
        next.Amount = next.Tariff;
        next.Source = "Касса";
        next.PaidAt = _today;
        db.SaveChanges();
        Load();
        Status = $"Внесена оплата {next.Tariff:N2} ₽ за {Months[next.Month - 1]} 2026. Окно подтверждения от 1000 ₽ не показывается.";
    }

    [RelayCommand]
    private void AcceptTicket()
    {
        if (SelectedHouse is null) return;
        using var db = _db.CreateDbContext();
        var num = db.Tickets.Max(t => t.Number) + 1;
        db.Tickets.Add(new WorkTicket
        {
            Number = num,
            Date = _today,
            Time = TimeOnly.FromDateTime(DateTime.Now),
            HouseId = SelectedHouse.Id,
            SubscriberId = SelectedSubscriber?.Id,
            Apartment = SelectedSubscriber?.Apartment ?? FindApartment,
            Section = SelectedSubscriber?.Section ?? FindSection,
            Phone = SelectedSubscriber?.Phone ?? "",
            Kind = TicketKind.Antenna,
            Fault = string.IsNullOrWhiteSpace(TicketFault) ? "Принята с рабочего места диспетчера" : TicketFault,
            Status = TicketStatus.Accepted,
            AcceptedBy = User?.DisplayName ?? "",
            RisersNote = ""
        });
        db.SaveChanges();
        Load();
        Status = $"Принята заявка № {num}.";
        Module = AppModule.Disp;
    }

    [RelayCommand]
    private void SaveTicket()
    {
        if (SelectedTicket is null) return;
        using var db = _db.CreateDbContext();
        var t = db.Tickets.First(x => x.Id == SelectedTicket.Id);
        t.Fault = TicketFault;
        t.WorkDone = TicketWork;
        t.Mechanic = TicketMechanic;
        if (!string.IsNullOrWhiteSpace(TicketWork) && t.Status != TicketStatus.Done)
            t.Status = TicketStatus.Done;
        db.SaveChanges();
        Load();
        Status = "Заявка записана.";
        Module = AppModule.Disp;
    }

    [RelayCommand]
    private void SaveHouse()
    {
        if (SelectedHouse is null) return;
        using var db = _db.CreateDbContext();
        var h = db.Houses.First(x => x.Id == SelectedHouse.Id);
        h.Antennas = SelectedHouse.Antennas;
        h.AerialCables = SelectedHouse.AerialCables;
        h.Amplifiers = SelectedHouse.Amplifiers;
        h.PowerSupplies = SelectedHouse.PowerSupplies;
        h.TechFloorKey = SelectedHouse.TechFloorKey;
        h.OtherNotes = SelectedHouse.OtherNotes;
        db.SaveChanges();
        Status = "Паспорт дома сохранён в локальную базу.";
    }

    [RelayCommand]
    private void Saved() => Status = "Сохранено в локальную базу Аверса (файл avers.db). Печать и выгрузка будут подключены вместе с настоящей СУБД.";

    private static string Money(decimal value) => value.ToString("N2") + " ₽";
}

public sealed class PaymentGridRow
{
    public PaymentGridRow(string title, string[] cells)
    {
        Title = title;
        var c = cells.Concat(Enumerable.Repeat("", 12)).Take(12).ToArray();
        Jan = c[0]; Feb = c[1]; Mar = c[2]; Apr = c[3]; May = c[4]; Jun = c[5];
        Jul = c[6]; Aug = c[7]; Sep = c[8]; Oct = c[9]; Nov = c[10]; Dec = c[11];
    }
    public string Title { get; }
    public string Jan { get; }
    public string Feb { get; }
    public string Mar { get; }
    public string Apr { get; }
    public string May { get; }
    public string Jun { get; }
    public string Jul { get; }
    public string Aug { get; }
    public string Sep { get; }
    public string Oct { get; }
    public string Nov { get; }
    public string Dec { get; }
}

public sealed class DebtorRow
{
    public DebtorRow(Subscriber s, decimal debt, int unpaid, bool old)
    {
        Subscriber = s;
        FullName = s.FullName;
        Address = $"{s.House.AddressLine}, кв. {s.Apartment}";
        Debt = debt;
        DebtText = debt.ToString("N2") + " ₽";
        Unpaid = unpaid;
        LastPay = s.LastPaymentDate is { } d ? d.ToString("dd.MM.yyyy") + (old ? " (≥ 3 лет)" : "") : "нет";
        Case = s.CourtCases.FirstOrDefault() is { } c ? $"{c.DelaAversId} · {c.OrderNumber}" : "—";
        IsDebt = debt >= Billing.DebtHighlight;
        IsOld = old && !IsDebt;
    }
    public Subscriber Subscriber { get; }
    public string FullName { get; }
    public string Address { get; }
    public decimal Debt { get; }
    public string DebtText { get; }
    public int Unpaid { get; }
    public string LastPay { get; }
    public string Case { get; }
    public bool IsDebt { get; }
    public bool IsOld { get; }
}

public sealed class PprRow
{
    public PprRow(House h)
    {
        Address = h.AddressLine;
        Floors = h.Floors.ToString();
        Entrances = h.Entrances.ToString();
        string Cell(int m) => Billing.PprTitle(h.Ppr.FirstOrDefault(p => p.Month == m)?.Status ?? PprStatus.Empty);
        Jan = Cell(1); Feb = Cell(2); Mar = Cell(3); Apr = Cell(4); May = Cell(5); Jun = Cell(6);
        Jul = Cell(7); Aug = Cell(8); Sep = Cell(9); Oct = Cell(10); Nov = Cell(11); Dec = Cell(12);
    }
    public string Address { get; }
    public string Floors { get; }
    public string Entrances { get; }
    public string Jan { get; }
    public string Feb { get; }
    public string Mar { get; }
    public string Apr { get; }
    public string May { get; }
    public string Jun { get; }
    public string Jul { get; }
    public string Aug { get; }
    public string Sep { get; }
    public string Oct { get; }
    public string Nov { get; }
    public string Dec { get; }
}

public sealed class AccessRow
{
    public static AccessRow From(UserRole role, string title) => new()
    {
        Role = title,
        Pay = Cell(role, AppModule.Abon),
        Account = role is UserRole.Administrator or UserRole.AdsHead ? "да" : "нет",
        Tickets = Cell(role, AppModule.Disp),
        House = Cell(role, AppModule.Anten),
        Scheme = Cell(role, AppModule.Scheme),
        Legal = Cell(role, AppModule.Legal),
        Dict = Cell(role, AppModule.Phonebook)
    };

    private static string Cell(UserRole role, AppModule m) =>
        RoleCatalog.Can(role, m, out var level) ? level switch
        {
            RoleCatalog.AccessLevel.Edit => "да",
            RoleCatalog.AccessLevel.View => "смотр",
            RoleCatalog.AccessLevel.ScreenOnly => "экран",
            _ => "нет"
        } : "нет";

    public string Role { get; init; } = "";
    public string Pay { get; init; } = "";
    public string Account { get; init; } = "";
    public string Tickets { get; init; } = "";
    public string House { get; init; } = "";
    public string Scheme { get; init; } = "";
    public string Legal { get; init; } = "";
    public string Dict { get; init; } = "";
}

public sealed class ReportMetric(string name, string value, bool on)
{
    public string Name { get; } = name;
    public string Value { get; } = value;
    public bool On { get; set; } = on;
}
