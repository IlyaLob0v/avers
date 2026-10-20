using System.Collections.ObjectModel;
using Avers.Contracts;
using Avers.Data;
using Avers.Desktop.Controls;
using Avers.Desktop.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAversStore _store;
    private readonly DateOnly _today;
    private List<House> _houses = [];
    /// <summary>Абоненты выбранного дома: грузятся при выборе дома, вся база в память не читается.</summary>
    private List<Subscriber> _subscribers = [];
    private bool _seesSubscribers;
    private Dictionary<int, decimal> _debtByHouse = [];
    private List<WorkTicket> _tickets = [];
    private List<IntercomTicket> _intercomTickets = [];
    private bool _holdRefresh;
    private int? _intercomHouseId;

    public MainViewModel(IAversStore store)
    {
        _store = store;
        _today = store.Today;
        _reportFrom = new DateOnly(_today.Year, 1, 1);
        _reportTo = _today;
    }

    public ObservableCollection<AppUser> Users { get; } = [];
    public ObservableCollection<Street> Streets { get; } = [];
    public ObservableCollection<House> HousesOnStreet { get; } = [];
    public ObservableCollection<WorkTicket> Tickets { get; } = [];
    public ObservableCollection<WorkTicket> HouseTickets { get; } = [];
    public ObservableCollection<Mechanic> Mechanics { get; } = [];
    public ObservableCollection<Material> Materials { get; } = [];
    public ObservableCollection<UnifiedReceipt> Receipts { get; } = [];
    public ObservableCollection<DebtorRow> Debtors { get; } = [];
    public ObservableCollection<PprRow> PprRows { get; } = [];
    public ObservableCollection<AccessRow> AccessRows { get; } = [];
    public ObservableCollection<ReportMetric> ReportMetrics { get; } = [];
    public ObservableCollection<HouseScheme> Schemes { get; } = [];
    public ObservableCollection<IntercomTicket> IntercomTickets { get; } = [];
    public ObservableCollection<PaymentGridRow> PaymentRows { get; } = [];

    [ObservableProperty] private AppUser? _loginUser;
    [ObservableProperty] private string _loginName = "";
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
    [ObservableProperty] private string _findStreet = DemoSeeder.StartStreet;
    [ObservableProperty] private string _findHouse = DemoSeeder.StartHouse;
    [ObservableProperty] private string _findSection = DemoSeeder.StartSection;
    [ObservableProperty] private string _findApartment = DemoSeeder.StartApartment;
    [ObservableProperty] private string _findQuery = "";
    [ObservableProperty] private string _status = "Готово.";
    [ObservableProperty] private bool _onlyActiveContracts = true;
    [ObservableProperty] private string _ticketFault = "";
    [ObservableProperty] private string _ticketWork = "";
    [ObservableProperty] private string _ticketMechanic = "";
    [ObservableProperty] private string _claimText = "";
    [ObservableProperty] private DateOnly _reportFrom;
    [ObservableProperty] private DateOnly _reportTo;
    [ObservableProperty] private IntercomPanel? _intercom;

    public string LoginRoleTitle => LoginUser is null ? "" : RoleCatalog.Title(LoginUser.Role);
    public bool ShowLogin => User is null;
    public bool ShowShell => User is not null;
    public bool IsShared => _store.IsShared;
    public string StoreTitle => _store.Title;

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
        AppModule.Reports => "Отчёты · Excel",
        AppModule.Phonebook => "Словарь",
        AppModule.Domofo => SelectedHouse is null ? "Домофон" : $"Домофон · {SelectedHouse.AddressLine}",
        AppModule.Reader => "Просмотр дома — без правки",
        AppModule.Access => "Права доступа",
        AppModule.ChangeLog => "Журнал изменений",
        _ => "Аверс"
    };

    public string WorkDateText => _today.ToString("dd.MM.yyyy");
    public string FaultBanner => SelectedHouse is { OpenFault: not null } h
        ? $"Неустранённая неисправность СКПТ: {h.OpenFault}. Причина: {h.OpenFaultReason}. План устранения: {h.OpenFaultPlannedFix:dd.MM.yyyy}."
        : "";
    public bool HasFault => !string.IsNullOrWhiteSpace(FaultBanner);
    public string ChargedText => Money(SelectedSubscriber is null ? 0 : Billing.Charged(DuePayments));
    public string PaidText => Money(SelectedSubscriber is null ? 0 : Billing.Paid(DuePayments));
    public string DebtText => Money(SelectedSubscriber is null ? 0 : Billing.Debt(DuePayments));
    public bool HasDebt => SelectedSubscriber is not null && Billing.Debt(DuePayments) > 0;
    private List<YearPayment> DuePayments => Billing.Due(SelectedSubscriber?.Payments ?? [], _today);
    public string ToPayText => SelectedSubscriber?.Payments.FirstOrDefault(p => p.Year == _today.Year && p.Month == _today.Month) is { } p
        ? Money(p.Tariff)
        : "";
    public string PaymentsCaption => $"Платежи {_today.Year} · тариф / отв. / сумма / дата опл.";
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
    public IReadOnlyList<string> Months { get; } = ["Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек"];

    /// <summary>Экран входа: в учебной базе — список пользователей, на сервере логин вводят руками.</summary>
    public void Start()
    {
        Users.Clear();
        foreach (var u in _store.LoginUsers())
            Users.Add(u);
        LoginUser = Users.FirstOrDefault(x => x.Login == DemoSeeder.StartLogin) ?? Users.FirstOrDefault();

        AccessRows.Clear();
        foreach (var (role, title) in RoleCatalog.All)
            AccessRows.Add(AccessRow.From(role, title));
    }

    /// <summary>Перечитать данные после записи: своя запись и чужие изменения в общей базе.</summary>
    private async Task ReloadAsync() => await Apply(await _store.LoadAsync(User!), User!);

    private async Task Apply(AversSnapshot data, AppUser user)
    {
        var keepSubscriberId = SelectedSubscriber?.Id;
        var keepHouseId = SelectedHouse?.Id;
        _seesSubscribers = RoleCatalog.Can(user.Role, AppModule.Abon, out _);
        PaymentWarningMonths = data.PaymentWarningMonths;

        Streets.Clear();
        foreach (var s in data.Streets)
            Streets.Add(s);

        _houses = data.Houses;
        _tickets = data.Tickets;
        _intercomTickets = data.IntercomTickets;
        LoadTicketYears();

        Mechanics.Clear();
        foreach (var m in data.Mechanics) Mechanics.Add(m);
        Materials.Clear();
        foreach (var m in data.Materials) Materials.Add(m);
        Organizations.Clear();
        foreach (var o in data.Organizations) Organizations.Add(o);

        RefreshTotals();

        // После записи остаёмся на том же доме и квартире; экраны пересчитываем один раз, в конце.
        var keep = _houses.FirstOrDefault(h => h.Id == keepHouseId);
        _holdRefresh = true;
        SelectedStreet = Streets.FirstOrDefault(s => s.Id == keep?.StreetId)
                         ?? Streets.FirstOrDefault(s => s.Name == DemoSeeder.StartStreet) ?? Streets.FirstOrDefault();
        RefreshHouses();
        SelectedHouse = HousesOnStreet.FirstOrDefault(h => h.Id == keep?.Id)
                        ?? HousesOnStreet.FirstOrDefault(h => h.Number == DemoSeeder.StartHouse) ?? HousesOnStreet.FirstOrDefault();
        SelectedTicket = _tickets.FirstOrDefault(t => t.Number == DemoSeeder.StartTicket);
        _holdRefresh = false;
        _intercomHouseId = null;
        BindTicket();
        await ShowHouseAsync(keepSubscriberId);
    }

    /// <summary>Загружает абонентов выбранного дома и выбирает квартиру: нужную, учебную стартовую или первую.</summary>
    private async Task ShowHouseAsync(int? subscriberId)
    {
        var house = SelectedHouse;
        List<Subscriber> list = [];
        if (house is not null && _seesSubscribers)
        {
            try
            {
                list = await _store.LoadHouseAsync(house);
            }
            catch (StoreException e)
            {
                Status = e.Message;
            }
        }
        // Пока грузили, сотрудник мог выбрать другой дом.
        if (SelectedHouse != house) return;

        _subscribers = list;
        _holdRefresh = true;
        SelectedSubscriber = list.FirstOrDefault(s => s.Id == subscriberId)
                             ?? list.FirstOrDefault(s => s.Account == DemoSeeder.StartAccount) ?? list.FirstOrDefault();
        _holdRefresh = false;
        RefreshDerived();
    }

    /// <summary>Переходит к дому абонента и выбирает его квартиру.</summary>
    private async Task ShowSubscriberAsync(int subscriberId, int houseId)
    {
        var house = _houses.First(h => h.Id == houseId);
        _holdRefresh = true;
        SelectedStreet = Streets.First(s => s.Id == house.StreetId);
        SelectedHouse = house;
        _holdRefresh = false;
        await ShowHouseAsync(subscriberId);
    }

    /// <summary>Должники и долги по домам: сервер считает их по всей базе, когда открывают юристов или отчёты.</summary>
    private async Task LoadDebtsAsync()
    {
        if (!_seesSubscribers) return;
        Debts debts;
        try
        {
            debts = await _store.LoadDebtsAsync();
        }
        catch (StoreException e)
        {
            Status = e.Message;
            return;
        }
        var houses = _houses.ToDictionary(h => h.Id);
        var keepDebtor = SelectedDebtor?.SubscriberId;
        Debtors.Clear();
        foreach (var d in debts.Debtors)
            Debtors.Add(new DebtorRow(d, houses[d.HouseId]));
        SelectedDebtor = Debtors.FirstOrDefault(d => d.SubscriberId == keepDebtor) ?? Debtors.FirstOrDefault();
        _debtByHouse = debts.ByHouse;
        RefreshReport();
    }

    partial void OnLoginUserChanged(AppUser? value) => OnPropertyChanged(nameof(LoginRoleTitle));

    partial void OnUserChanged(AppUser? value)
    {
        OnPropertyChanged(nameof(ShowLogin));
        OnPropertyChanged(nameof(ShowShell));
        OnPropertyChanged(nameof(CanEditCard));
        OnPropertyChanged(nameof(CanEditSettings));
        OnPropertyChanged(nameof(CanEditPpr));
        OnPropertyChanged(nameof(CanEditSchemes));
        OnPropertyChanged(nameof(CanEditPhonebook));
        NotifyTicketPanel();
        BindTicket();
        RebuildNavigation();
        OnPropertyChanged(nameof(UserLine));
        OnPropertyChanged(nameof(UserRoleTitle));
        OnPropertyChanged(nameof(WindowTitle));
    }

    partial void OnModuleChanged(AppModule value)
    {
        MarkCurrent();
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ModuleTitle));
        OnPropertyChanged(nameof(ModuleLegacyName));
        OnPropertyChanged(nameof(IsOnDesktop));
        if (value == AppModule.Access)
            _ = LoadEmployeesAsync();
        if (value == AppModule.ChangeLog)
            _ = LoadChangeLogAsync();
        if (value == AppModule.Scheme)
            _ = ShowSchemeAsync();
        if (value is AppModule.Legal or AppModule.Claim or AppModule.Reports)
            _ = LoadDebtsAsync();
    }

    partial void OnSelectedStreetChanged(Street? value)
    {
        StreetName = value?.Name ?? "";
        RefreshHouses();
        SelectedHouse = HousesOnStreet.FirstOrDefault();
    }

    partial void OnSelectedHouseChanged(House? value)
    {
        EditHouse = value;
        if (!_holdRefresh)
            _ = ShowHouseAsync(null);
    }

    partial void OnSelectedSubscriberChanged(Subscriber? value)
    {
        PrintWithPayDate = value?.PrintAccountWithPayDate ?? true;
        RefreshDerived();
    }

    partial void OnSelectedTicketChanged(WorkTicket? value) => BindTicket();
    partial void OnSelectedDebtorChanged(DebtorRow? value) => BuildClaim();
    partial void OnOnlyActiveContractsChanged(bool value) => RefreshDerived();

    private void RefreshHouses()
    {
        HousesOnStreet.Clear();
        if (SelectedStreet is null) return;
        foreach (var h in _houses.Where(x => x.StreetId == SelectedStreet.Id).OrderBy(x => x.Number, NaturalOrder.Instance))
            HousesOnStreet.Add(h);
    }

    /// <summary>Сводки по всей базе (ППР, отчёт): меняются только после записи, считаем их в <see cref="Apply"/>.</summary>
    private void RefreshTotals()
    {
        LoadPprYears();

        RefreshReport();
    }

    /// <summary>Всё, что зависит от выбранного дома и квартиры.</summary>
    private void RefreshDerived()
    {
        if (_holdRefresh) return;
        RefreshTickets();
        RebuildPayments();
        RebuildReceiptPeriods();

        if (_intercomHouseId != SelectedHouse?.Id)
        {
            _intercomHouseId = SelectedHouse?.Id;
            Schemes.Clear();
            IntercomTickets.Clear();
            if (SelectedHouse is not null)
            {
                foreach (var s in SelectedHouse.Schemes) Schemes.Add(s);
                foreach (var t in _intercomTickets.Where(x => x.HouseId == SelectedHouse.Id))
                    IntercomTickets.Add(t);
            }
            SelectedScheme = Schemes.FirstOrDefault();
        }
        Intercom = SelectedHouse?.Intercoms.FirstOrDefault();

        OnPropertyChanged(nameof(FaultBanner));
        OnPropertyChanged(nameof(HasFault));
        OnPropertyChanged(nameof(ChargedText));
        OnPropertyChanged(nameof(PaidText));
        OnPropertyChanged(nameof(DebtText));
        OnPropertyChanged(nameof(ReceiptMonthsText));
        OnPropertyChanged(nameof(ReceiptSumText));
        OnPropertyChanged(nameof(HasDebt));
        OnPropertyChanged(nameof(ToPayText));
        OnPropertyChanged(nameof(OekWarning));
        OnPropertyChanged(nameof(HouseCaption));
        OnPropertyChanged(nameof(HouseStats));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ContractDateText));
        OnPropertyChanged(nameof(LastPprText));
        OnPropertyChanged(nameof(Intercom));
        OnPropertyChanged(nameof(HouseAptLine));
        NotifyTicketPanel();
        RefreshCard();
        BuildClaim();
    }

    private void RebuildPayments()
    {
        PaymentRows.Clear();
        var pays = SelectedSubscriber?.Payments.Where(p => p.Year == _today.Year).OrderBy(p => p.Month).ToList() ?? [];
        PaymentRows.Add(new PaymentGridRow("Тариф", pays.Select(p => p.Tariff.ToString("N2")).ToArray()));
        PaymentRows.Add(new PaymentGridRow("Отв.", pays.Select(p => p.Source ?? "").ToArray()));
        PaymentRows.Add(new PaymentGridRow("Сумма", pays.Select(p => p.Amount is null ? "" : p.Amount.Value.ToString("N2")).ToArray()));
        PaymentRows.Add(new PaymentGridRow("Дата опл.", pays.Select(p => p.PaidAt is null ? "" : p.PaidAt.Value.ToString("dd.MM")).ToArray()));
        PaymentRows.Add(new PaymentGridRow("Внёс", pays.Select(p => p.RecordedBy ?? "").ToArray()));
    }

    private void BuildClaim()
    {
        var row = SelectedDebtor ?? Debtors.FirstOrDefault(d => d.Unpaid >= Billing.ClaimUnpaidMonths);
        if (row is null) { ClaimText = ""; return; }
        ClaimText =
            $"Досудебное уведомление\n\n{row.FullName}, л/с {row.Account}, {row.Address}.\n\n" +
            $"Задолженность за ТО СКПТ за {row.Unpaid} мес. — {row.DebtText}. Предлагаем погасить долг в течение 30 дней. По истечении срока материалы будут направлены в суд.\n\n" +
            $"{_today:dd.MM.yyyy}, антенная служба";
        OnPropertyChanged(nameof(ClaimText));
    }

    [RelayCommand]
    private async Task Login()
    {
        LoginError = null;
        var login = IsShared ? LoginName.Trim() : LoginUser?.Login;
        if (string.IsNullOrEmpty(login)) { LoginError = IsShared ? "Введите логин." : "Выберите пользователя."; return; }
        try
        {
            var user = await _store.SignInAsync(login, Password);
            await Apply(await _store.LoadAsync(user), user);
            User = user;
        }
        catch (StoreException e)
        {
            _store.SignOut();
            LoginError = e.Message;
            return;
        }
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
        _store.SignOut();
        User = null;
        Debtors.Clear();
        _debtByHouse = [];
        _history.Clear();
        BackCommand.NotifyCanExecuteChanged();
        Module = AppModule.Desktop;
        Status = "Сеанс завершён.";
    }

    [RelayCommand]
    private void Go(AppModule page)
    {
        if (User is null) return;
        if (page != Module)
            Remember(Module);
        Module = page;
        var check = page switch
        {
            AppModule.Ticket => AppModule.Disp,
            AppModule.Subscriber or AppModule.Print or AppModule.PrDubl => AppModule.Abon,
            AppModule.Scheme or AppModule.AntenInfo or AppModule.Ppr => AppModule.Anten,
            AppModule.Claim => AppModule.Legal,
            _ => page
        };
        if (page is not AppModule.Desktop && User.Role != UserRole.Administrator && !RoleCatalog.Can(User.Role, check, out _))
            Status = $"Макет окна открыт. Для роли «{RoleCatalog.Title(User.Role)}» это место в работе будет с ограниченными правами.";
        else
            Status = WindowTitle;
    }

    [RelayCommand]
    private void SetSearch(SearchMode mode) => SearchMode = mode;

    /// <summary>Ищет сервер (или учебная база); программа переходит к дому найденного абонента.</summary>
    [RelayCommand]
    private async Task Find()
    {
        try
        {
            var hit = await _store.FindSubscriberAsync(SearchMode, FindQuery, FindStreet, FindHouse, FindApartment);
            if (hit is null) { Status = "Абонент не найден."; return; }
            await ShowSubscriberAsync(hit.Value.Id, hit.Value.HouseId);
        }
        catch (StoreException e)
        {
            Status = e.Message;
            return;
        }
        if (SelectedSubscriber is { } found)
            Status = $"Найден {found.FullName}, кв. {found.Apartment}.";
    }

    [RelayCommand]
    private async Task RecordPayment()
    {
        if (SelectedSubscriber is null || User is null) return;
        if (User is { CanEditAccount: false, Role: not UserRole.AdsHead and not UserRole.Administrator })
        {
            Status = "Нет права вносить оплату.";
            return;
        }
        await Save(async () =>
        {
            var paid = await _store.RecordPaymentAsync(SelectedSubscriber.Id, _today.Year, User);
            await ReloadAsync();
            Status = $"Внесена оплата {paid.Amount:N2} ₽ за {Months[paid.Month - 1]} {paid.Year}.";
        });
    }

    [RelayCommand]
    private async Task SaveHouse()
    {
        if (SelectedHouse is not { } h) return;
        await Save(async () =>
        {
            await _store.SaveHousePassportAsync(h.Id, new HousePassportRequest(
                h.Antennas, h.AerialCables, h.Amplifiers, h.PowerSupplies, h.TechFloorKey, h.OtherNotes));
            Status = "Паспорт дома сохранён.";
        });
    }

    /// <summary>Запись в базу: отказ сервера или нет связи — в строку состояния, программа продолжает работать.</summary>
    private async Task Save(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (StoreException e)
        {
            Status = e.Message;
        }
    }

    /// <summary>Кнопка экрана-макета: действие ещё не реализовано, говорим об этом прямо.</summary>
    [RelayCommand]
    private void NotReady(string? action) => Status = $"«{action}» пока не работает: функция в разработке.";

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

/// <summary>Строка списка должников: красная — долг от <see cref="Billing.DebtHighlight"/> ₽, жёлтая — давно не платил.</summary>
public sealed class DebtorRow : IRowTone
{
    public DebtorRow(Debtor d, House house)
    {
        SubscriberId = d.SubscriberId;
        Account = d.Account;
        FullName = d.FullName;
        Address = $"{house.AddressLine}, кв. {d.Apartment}";
        Debt = d.Debt;
        DebtText = d.Debt.ToString("N2") + " ₽";
        Unpaid = d.Unpaid;
        LastPay = d.LastPaymentDate is { } date ? date.ToString("dd.MM.yyyy") + (d.IsOld ? " (≥ 3 лет)" : "") : "нет";
        Case = d.Case ?? "—";
        IsDebt = d.Debt >= Billing.DebtHighlight;
        IsOld = d.IsOld && !IsDebt;
    }
    public int SubscriberId { get; }
    public string Account { get; }
    public string FullName { get; }
    public string Address { get; }
    public decimal Debt { get; }
    public string DebtText { get; }
    public int Unpaid { get; }
    public string LastPay { get; }
    public string Case { get; }
    public bool IsDebt { get; }
    public bool IsOld { get; }
    public RowTone Tone => IsDebt ? RowTone.Debt : IsOld ? RowTone.Warn : RowTone.None;
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
