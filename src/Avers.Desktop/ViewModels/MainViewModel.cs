using System.Collections.ObjectModel;
using System.Globalization;
using Avers.Contracts;
using Avers.Data;
using Avers.Desktop.Controls;
using Avers.Desktop.Data;
using Avers.Desktop.Printing;
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
    /// <summary>Видит ли сотрудник заявки (модуль «Заявки»): только тогда программа их запрашивает.</summary>
    private bool _seesTickets;
    /// <summary>Заявки, принятые сегодня: принято и выполнено — для предупреждения о списке ОЭК.</summary>
    private (int Accepted, int Done) _todayTickets;
    private int? _houseTicketsOf;
    private int _houseTicketsVersion;
    private List<IntercomTicket> _intercomTickets = [];
    private bool _holdRefresh;
    private int? _intercomHouseId;

    public MainViewModel(IAversStore store)
    {
        _store = store;
        _today = store.Today;
        AccessScreen = new AccessViewModel(this);
        ChangeLogScreen = new ChangeLogViewModel(this);
        PhonebookScreen = new PhonebookViewModel(this);
        PprScreen = new PprViewModel(this);
        IntercomScreen = new IntercomViewModel(this);
        SchemeScreen = new SchemeViewModel(this);
        ReportsScreen = new ReportsViewModel(this);
        ReceiptsScreen = new ReceiptsViewModel(this);
        LegalScreen = new LegalViewModel(this);
        ContractsScreen = new ContractsViewModel(this);
        TicketsScreen = new TicketsViewModel(this);
    }

    internal IAversStore Store => _store;
    internal DateOnly Today => _today;
    internal IReadOnlyList<House> Houses => _houses;
    internal IReadOnlyList<Subscriber> HouseSubscribers => _subscribers;
    internal IReadOnlyList<IntercomTicket> AllIntercomTickets => _intercomTickets;
    internal bool SeesTickets => _seesTickets;
    internal IReadOnlyDictionary<int, decimal> DebtByHouse => _debtByHouse;

    public AccessViewModel AccessScreen { get; }
    public ChangeLogViewModel ChangeLogScreen { get; }
    public PhonebookViewModel PhonebookScreen { get; }
    public PprViewModel PprScreen { get; }
    public IntercomViewModel IntercomScreen { get; }
    public SchemeViewModel SchemeScreen { get; }
    public ReportsViewModel ReportsScreen { get; }
    public ReceiptsViewModel ReceiptsScreen { get; }
    public LegalViewModel LegalScreen { get; }
    public ContractsViewModel ContractsScreen { get; }
    public TicketsViewModel TicketsScreen { get; }

    public ObservableCollection<AppUser> Users { get; } = [];
    public ObservableCollection<Street> Streets { get; } = [];
    public ObservableCollection<House> HousesOnStreet { get; } = [];
    public ObservableCollection<WorkTicket> HouseTickets { get; } = [];
    public ObservableCollection<Mechanic> Mechanics { get; } = [];
    public ObservableCollection<Material> Materials { get; } = [];
    public ObservableCollection<PaymentGridRow> PaymentRows { get; } = [];

    [ObservableProperty] private AppUser? _loginUser;
    [ObservableProperty] private string _loginName = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string? _loginError;
    /// <summary>Предупреждение на экране входа: адрес сервера без https://.</summary>
    [ObservableProperty] private string? _connectionWarning;
    [ObservableProperty] private AppUser? _user;
    [ObservableProperty] private AppModule _module = AppModule.Desktop;
    [ObservableProperty] private Street? _selectedStreet;
    [ObservableProperty] private House? _selectedHouse;
    [ObservableProperty] private Subscriber? _selectedSubscriber;
    [ObservableProperty] private SearchMode _searchMode = SearchMode.Address;
    [ObservableProperty] private string _findStreet = DemoSeeder.StartStreet;
    [ObservableProperty] private string _findHouse = DemoSeeder.StartHouse;
    [ObservableProperty] private string _findSection = DemoSeeder.StartSection;
    [ObservableProperty] private string _findApartment = DemoSeeder.StartApartment;
    [ObservableProperty] private string _findQuery = "";
    [ObservableProperty] private string _status = "Готово.";

    public string LoginRoleTitle => LoginUser is null ? "" : RoleCatalog.Title(LoginUser.Role);
    public bool ShowLogin => User is null;
    public bool ShowShell => User is not null;
    public bool IsShared => _store.IsShared;
    public string StoreTitle => _store.Title;

    public string WindowTitle => Module switch
    {
        AppModule.Abon => $"Ввод платежей абонентов за {_today:dd.MM.yyyy}",
        AppModule.Disp => $"Приём заявок от населения · {_today:dd.MM.yyyy}",
        AppModule.Ticket => TicketsScreen.SelectedTicket is not { } t ? "Заявка" : $"Заявка № {t.Number} от {t.Date:dd.MM.yyyy}",
        AppModule.Anten => SelectedHouse is null ? "Паспорт дома" : $"Паспорт дома · {SelectedHouse.AddressLine}",
        AppModule.AntenInfo => "Сведения по антенне / дому",
        AppModule.Scheme => SelectedHouse is null ? "Схема СКПТ" : $"Схема СКПТ · {SelectedHouse.AddressLine}",
        AppModule.Subscriber => SelectedSubscriber?.FullName ?? "Карточка абонента",
        AppModule.PrDubl => "Данные о платежах по единым квитанциям",
        AppModule.Contract => SelectedHouse is null ? "Договоры" : $"Договоры · {SelectedHouse.AddressLine}",
        AppModule.Print => "Квитанция на оплату ТО СКПТ",
        AppModule.Ppr => "Сервис → Графики ППР",
        AppModule.Legal => "Должники · юристы, суд",
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
          + (FaultOverdue ? " Срок устранения прошёл." : "")
        : "";
    public bool FaultOverdue => SelectedHouse is { OpenFault: not null, OpenFaultPlannedFix: { } plan } && plan < _today;
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
            var (accepted, done) = _todayTickets;
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
    }

    /// <summary>Перечитать данные после записи: своя запись и чужие изменения в общей базе.</summary>
    internal async Task ReloadAsync() => await Apply(await _store.LoadAsync(User!), User!);

    private async Task Apply(AversSnapshot data, AppUser user)
    {
        var keepSubscriberId = SelectedSubscriber?.Id;
        var keepHouseId = SelectedHouse?.Id;
        _seesSubscribers = RoleCatalog.Can(user.Role, AppModule.Abon, out _);
        _seesTickets = RoleCatalog.Can(user.Role, AppModule.Disp, out _);
        PaymentWarningMonths = data.PaymentWarningMonths;
        PaymentConfirmFrom = data.PaymentConfirmFrom;

        Streets.Clear();
        foreach (var s in data.Streets)
            Streets.Add(s);

        _houses = data.Houses;
        _intercomTickets = data.IntercomTickets;
        // Заявок в снимке нет: уже загруженные строки только переставляем на новые дома.
        LinkTickets(TicketsScreen.Tickets);
        LinkTickets(HouseTickets);
        LinkTickets(_apartmentTickets);

        Mechanics.Clear();
        foreach (var m in data.Mechanics) Mechanics.Add(m);
        Materials.Clear();
        foreach (var m in data.Materials) Materials.Add(m);
        PhonebookScreen.Organizations.Clear();
        foreach (var o in data.Organizations) PhonebookScreen.Organizations.Add(o);

        RefreshTotals();

        // После записи остаёмся на том же доме и квартире; экраны пересчитываем один раз, в конце.
        var keep = _houses.FirstOrDefault(h => h.Id == keepHouseId);
        _holdRefresh = true;
        SelectedStreet = Streets.FirstOrDefault(s => s.Id == keep?.StreetId)
                         ?? Streets.FirstOrDefault(s => s.Name == DemoSeeder.StartStreet) ?? Streets.FirstOrDefault();
        RefreshHouses();
        SelectedHouse = HousesOnStreet.FirstOrDefault(h => h.Id == keep?.Id)
                        ?? HousesOnStreet.FirstOrDefault(h => h.Number == DemoSeeder.StartHouse) ?? HousesOnStreet.FirstOrDefault();
        _holdRefresh = false;
        _intercomHouseId = null;
        TicketsScreen.BindTicket();
        await ShowHouseAsync(keepSubscriberId);
    }

    /// <summary>
    /// После входа: годы журнала, журнал со сводкой и итоги дня. Стартовая заявка учебной базы выбирается, если она в журнале.
    /// </summary>
    private async Task LoadTicketsAfterLoginAsync()
    {
        if (!_seesTickets) return;
        await TicketsScreen.LoadTicketYearsAsync();
        await TicketsScreen.RefreshTicketsAsync(force: false);
        TicketsScreen.SelectedTicket ??= TicketsScreen.Tickets.FirstOrDefault(t => t.Number == DemoSeeder.StartTicket);
        await LoadTodayTicketsAsync();
    }

    /// <summary>
    /// После записи заявки перечитываются только заявки на экранах: журнал со сводкой, заявки дома и квартиры,
    /// итоги дня. Снимок справочников не трогаем.
    /// </summary>
    internal async Task ReloadTicketsAsync()
    {
        await TicketsScreen.RefreshTicketsAsync(force: true);
        await LoadHouseTicketsAsync(force: true);
        await LoadApartmentTicketsAsync(force: true);
        await LoadTodayTicketsAsync();
    }

    /// <summary>Перед входом: заявки прежнего сотрудника забываются, всё запрашивается заново.</summary>
    private void ForgetTickets()
    {
        TicketsScreen.Forget();
        HouseTickets.Clear();
        _houseTicketsOf = null;
        _apartmentTickets = [];
        _apartmentTicketsOf = null;
        _todayTickets = (0, 0);
    }

    /// <summary>Связывает строки заявок с домами снимка (улица, организация).</summary>
    internal void LinkTickets(IEnumerable<WorkTicket> tickets)
    {
        var houses = _houses.ToDictionary(h => h.Id);
        foreach (var t in tickets)
            t.House = houses.GetValueOrDefault(t.HouseId) ?? t.House;
    }

    /// <summary>Заявки выбранного дома — для паспорта дома и числа невыполненных на экране приёма.</summary>
    private async Task LoadHouseTicketsAsync(bool force = false)
    {
        var house = SelectedHouse;
        if (!force && house?.Id == _houseTicketsOf) return;
        _houseTicketsOf = house?.Id;
        var version = ++_houseTicketsVersion;
        List<WorkTicket> list = [];
        if (house is not null && _seesTickets)
        {
            try
            {
                list = await _store.LoadTicketsAsync(new TicketFilter { HouseId = house.Id });
            }
            catch (StoreException e)
            {
                Status = e.Message;
                _houseTicketsOf = null;
            }
        }
        if (version != _houseTicketsVersion) return;
        LinkTickets(list);
        HouseTickets.Clear();
        foreach (var t in list)
            HouseTickets.Add(t);
        TicketsScreen.NotifyTicketPanel();
    }

    private async Task LoadTodayTicketsAsync()
    {
        if (!_seesTickets) return;
        try
        {
            var today = await _store.LoadTicketsByHouseAsync(_today, _today);
            _todayTickets = (today.Sum(s => s.Accepted), today.Sum(s => s.Done));
        }
        catch (StoreException e)
        {
            Status = e.Message;
        }
        OnPropertyChanged(nameof(OekWarning));
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
    internal async Task ShowSubscriberAsync(int subscriberId, int houseId)
    {
        var house = _houses.First(h => h.Id == houseId);
        _holdRefresh = true;
        SelectedStreet = Streets.First(s => s.Id == house.StreetId);
        SelectedHouse = house;
        _holdRefresh = false;
        await ShowHouseAsync(subscriberId);
    }

    /// <summary>Должники и долги по домам: сервер считает их по всей базе, когда открывают юристов или отчёты.</summary>
    internal async Task LoadDebtsAsync()
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
        LegalScreen.ShowDebtors(debts.Debtors.Select(d => new DebtorRow(d, houses[d.HouseId])));
        _debtByHouse = debts.ByHouse;
        ReportsScreen.RefreshReport();
    }

    partial void OnLoginUserChanged(AppUser? value) => OnPropertyChanged(nameof(LoginRoleTitle));

    partial void OnUserChanged(AppUser? value)
    {
        OnPropertyChanged(nameof(ShowLogin));
        OnPropertyChanged(nameof(ShowShell));
        OnPropertyChanged(nameof(CanEditCard));
        OnPropertyChanged(nameof(CanEditSettings));
        PprScreen.RefreshRights();
        SchemeScreen.RefreshRights();
        IntercomScreen.RefreshRights();
        PhonebookScreen.RefreshRights();
        LegalScreen.RefreshRights();
        TicketsScreen.NotifyTicketPanel();
        TicketsScreen.BindTicket();
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
        OnPropertyChanged(nameof(IsOnDesktop));
        if (value == AppModule.Access)
            _ = AccessScreen.LoadAsync();
        if (value == AppModule.ChangeLog)
            _ = ChangeLogScreen.LoadAsync();
        if (value == AppModule.Scheme)
            _ = SchemeScreen.ShowSchemeAsync();
        if (value is AppModule.Legal or AppModule.Claim or AppModule.Reports)
            _ = LoadDebtsAsync();
        if (value == AppModule.Reports)
            _ = ReportsScreen.LoadTicketCountsAsync();
    }

    partial void OnSelectedStreetChanged(Street? value)
    {
        PhonebookScreen.StreetName = value?.Name ?? "";
        RefreshHouses();
        SelectedHouse = HousesOnStreet.FirstOrDefault();
    }

    /// <summary>План устранения неисправности на паспорте дома, дд.мм.гггг.</summary>
    [ObservableProperty] private string _editFaultFix = "";

    partial void OnSelectedHouseChanged(House? value)
    {
        PhonebookScreen.EditHouse = value;
        EditFaultFix = value?.OpenFaultPlannedFix?.ToString("dd.MM.yyyy") ?? "";
        if (!_holdRefresh)
            _ = ShowHouseAsync(null);
    }

    partial void OnSelectedSubscriberChanged(Subscriber? value)
    {
        PrintWithPayDate = value?.PrintAccountWithPayDate ?? true;
        PaymentAmount = "";
        RefreshDerived();
    }


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
        PprScreen.LoadPprYears();

        ReportsScreen.RefreshReport();
    }

    /// <summary>Всё, что зависит от выбранного дома и квартиры.</summary>
    private void RefreshDerived()
    {
        if (_holdRefresh) return;
        TicketsScreen.RefreshTickets();
        _ = LoadHouseTicketsAsync();
        RebuildPayments();
        ReceiptsScreen.RebuildReceiptPeriods();

        var houseChanged = _intercomHouseId != SelectedHouse?.Id;
        if (houseChanged)
        {
            _intercomHouseId = SelectedHouse?.Id;
            SchemeScreen.ShowHouse(SelectedHouse);
        }
        IntercomScreen.Refresh(houseChanged);

        OnPropertyChanged(nameof(FaultBanner));
        OnPropertyChanged(nameof(HasFault));
        OnPropertyChanged(nameof(FaultOverdue));
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
        OnPropertyChanged(nameof(HouseAptLine));
        TicketsScreen.NotifyTicketPanel();
        RefreshCard();
        LegalScreen.BuildClaim();
    }

    internal void NotifyWindowTitle() => OnPropertyChanged(nameof(WindowTitle));

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

    [RelayCommand]
    private async Task Login()
    {
        LoginError = null;
        var login = IsShared ? LoginName.Trim() : LoginUser?.Login;
        if (string.IsNullOrEmpty(login)) { LoginError = IsShared ? "Введите логин." : "Выберите пользователя."; return; }
        try
        {
            var user = await _store.SignInAsync(login, Password);
            ForgetTickets();
            await Apply(await _store.LoadAsync(user), user);
            await LoadTicketsAfterLoginAsync();
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
        LegalScreen.Debtors.Clear();
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
        // Право на окно — как у кнопки модуля, которому оно принадлежит (NavCatalog).
        if (NavCatalog.Owner(page) is { } owner && !NavCatalog.IsAvailable(User.Role, owner))
        {
            Status = $"Раздел «{owner.Title}» для роли «{RoleCatalog.Title(User.Role)}» закрыт.";
            return;
        }
        if (page != Module)
            Remember(Module);
        Module = page;
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
        decimal? amount = null;
        if (!string.IsNullOrWhiteSpace(PaymentAmount))
        {
            if (!decimal.TryParse(PaymentAmount.Trim(), NumberStyles.Number, Ru, out var typed) || typed < 0)
            {
                Status = "Сумма оплаты — число, например 150 или 150,50.";
                return;
            }
            amount = typed;
        }
        var sum = amount ?? SelectedSubscriber.Payments.Where(p => p.Year == _today.Year && p.Amount is null)
            .OrderBy(p => p.Month).FirstOrDefault()?.Tariff ?? 0;
        var pending = (SelectedSubscriber.Id, sum);
        if (sum >= PaymentConfirmFrom && _paymentToConfirm != pending)
        {
            _paymentToConfirm = pending;
            Status = $"Внести {Money(sum)}? Нажмите «Внести оплату» ещё раз.";
            return;
        }
        _paymentToConfirm = null;
        await Save(async () =>
        {
            var paid = await _store.RecordPaymentAsync(SelectedSubscriber.Id, _today.Year, amount, User);
            await ReloadAsync();
            Status = $"Внесена оплата {paid.Amount:N2} ₽ за {Months[paid.Month - 1]} {paid.Year}.";
        });
    }

    [RelayCommand]
    private async Task SaveHouse()
    {
        if (SelectedHouse is not { } h) return;
        if (!TryParseDate(EditFaultFix, out var fix))
        {
            Status = "План устранения — дата в виде дд.мм.гггг.";
            return;
        }
        var org = h.Organization is { } o ? new HouseOrganizationRequest(o.Name, o.Branch, o.Address, o.Phones, o.HeadName, o.EngineerName) : null;
        await Save(async () =>
        {
            await _store.SaveHousePassportAsync(h.Id, new HousePassportRequest(
                h.Antennas, h.AerialCables, h.Amplifiers, h.PowerSupplies, h.TechFloorKey, h.OtherNotes,
                h.OpenFault, h.OpenFaultReason, fix, org));
            // Как сохранил сервер: пустая неисправность снимает причину и план.
            h.OpenFault = string.IsNullOrWhiteSpace(h.OpenFault) ? null : h.OpenFault.Trim();
            h.OpenFaultReason = h.OpenFault is null || string.IsNullOrWhiteSpace(h.OpenFaultReason) ? null : h.OpenFaultReason.Trim();
            h.OpenFaultPlannedFix = h.OpenFault is null ? null : fix;
            EditFaultFix = h.OpenFaultPlannedFix?.ToString("dd.MM.yyyy") ?? "";
            OnPropertyChanged(nameof(FaultBanner));
            OnPropertyChanged(nameof(HasFault));
            OnPropertyChanged(nameof(FaultOverdue));
            Status = "Паспорт дома сохранён.";
        });
    }

    /// <summary>Запись в базу: отказ сервера или нет связи — в строку состояния, программа продолжает работать.</summary>
    /// <summary>Выполняет запись; отказ хранилища показывает в строке состояния.</summary>
    internal async Task Save(Func<Task> action)
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

    internal static string Money(decimal value) => value.ToString("N2") + " ₽";
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

public sealed class ReportMetric(string name, string value, bool on)
{
    public string Name { get; } = name;
    public string Value { get; } = value;
    public bool On { get; set; } = on;
}
