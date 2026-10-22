using System.Collections.ObjectModel;
using System.Windows.Input;
using Avers.Contracts;
using Avers.Desktop.Printing;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>Чьи заявки показывает журнал АДС.</summary>
public enum TicketScope
{
    All,
    House,
    Apartment
}

public sealed record TicketOption<T>(T? Value, string Title) where T : struct
{
    public override string ToString() => Title;
}

/// <summary>Журнал заявок АДС: отбор по периоду, году, дому, квартире и виду; права начальника и пользователя АДС.</summary>
public sealed partial class TicketsViewModel(MainViewModel shell) : ObservableObject
{
    public MainViewModel Shell => shell;

    public ObservableCollection<WorkTicket> Tickets { get; } = [];
    public ObservableCollection<int> TicketYears { get; } = [];
    public ObservableCollection<TicketMonthRow> TicketMonths { get; } = [];

    public IReadOnlyList<TicketOption<TicketScope>> TicketScopes { get; } =
    [
        new(TicketScope.All, "Все дома"),
        new(TicketScope.House, "Выбранный дом"),
        new(TicketScope.Apartment, "Выбранная квартира")
    ];

    public IReadOnlyList<TicketOption<TicketKind>> TicketKinds { get; } =
        [new(null, "Все виды"), .. Enum.GetValues<TicketKind>().Select(k => new TicketOption<TicketKind>(k, Billing.KindTitle(k)))];

    public IReadOnlyList<TicketOption<TicketKind>> NewTicketKinds { get; } =
        [.. Enum.GetValues<TicketKind>().Select(k => new TicketOption<TicketKind>(k, Billing.KindTitle(k)))];

    public IReadOnlyList<TicketOption<TicketStatus>> TicketStatuses { get; } =
    [
        new(null, "Любые"),
        new(TicketStatus.Accepted, "Принятые"),
        new(TicketStatus.InProgress, "В работе"),
        new(TicketStatus.Deferred, "Отложенные"),
        new(TicketStatus.Done, "Выполненные")
    ];

    [ObservableProperty] private TicketOption<TicketScope> _scopeFilter = null!;
    [ObservableProperty] private TicketOption<TicketKind> _kindFilter = null!;
    [ObservableProperty] private TicketOption<TicketKind> _newTicketKind = null!;
    [ObservableProperty] private TicketOption<TicketStatus> _statusFilter = null!;
    [ObservableProperty] private bool _onlyOpenTickets;
    [ObservableProperty] private DateTime? _ticketFrom;
    [ObservableProperty] private DateTime? _ticketTo;
    [ObservableProperty] private int _ticketYear;
    [ObservableProperty] private string _ticketSearch = "";
    [ObservableProperty] private string _newTicketFault = "";
    [ObservableProperty] private WorkTicket? _selectedTicket;
    [ObservableProperty] private string _ticketFault = "";
    [ObservableProperty] private string _ticketWork = "";
    [ObservableProperty] private string _ticketMechanic = "";

    private bool _ticketFilterReady;
    private bool _holdTicketRefresh;

    private void EnsureTicketFilter()
    {
        if (_ticketFilterReady) return;
        _ticketFilterReady = true;
        _holdTicketRefresh = true;
        ScopeFilter = TicketScopes[0];
        KindFilter = TicketKinds[0];
        NewTicketKind = NewTicketKinds[0];
        StatusFilter = TicketStatuses[0];
        TicketYear = shell.Today.Year;
        TicketFrom = new DateTime(shell.Today.Year, 1, 1);
        TicketTo = new DateTime(shell.Today.Year, 12, 31);
        _holdTicketRefresh = false;
    }

    partial void OnSelectedTicketChanged(WorkTicket? value) => BindTicket();
    partial void OnScopeFilterChanged(TicketOption<TicketScope> value) => RefreshTickets();
    partial void OnKindFilterChanged(TicketOption<TicketKind> value) => RefreshTickets();
    partial void OnStatusFilterChanged(TicketOption<TicketStatus> value) => RefreshTickets();
    partial void OnOnlyOpenTicketsChanged(bool value) => RefreshTickets();
    partial void OnTicketFromChanged(DateTime? value) => RefreshTickets();
    partial void OnTicketToChanged(DateTime? value) => RefreshTickets();
    partial void OnTicketSearchChanged(string value) => RefreshTickets();
    partial void OnTicketYearChanged(int value)
    {
        if (!_holdTicketRefresh && value > 0)
            SetPeriod(new DateOnly(value, 1, 1), new DateOnly(value, 12, 31));
    }


    public string TicketCountText
    {
        get
        {
            var done = Tickets.Count(t => t.Status == TicketStatus.Done);
            return $"Всего заявок: {Tickets.Count} · выполнено {done} · не выполнено {Tickets.Count - done}";
        }
    }

    public string TicketPeriodText => (TicketFrom, TicketTo) switch
    {
        (null, null) => "за всё время",
        ({ } f, null) => $"с {f:dd.MM.yyyy}",
        (null, { } t) => $"по {t:dd.MM.yyyy}",
        ({ } f, { } t) => $"с {f:dd.MM.yyyy} по {t:dd.MM.yyyy}"
    };

    public string TicketMonthsCaption => ScopeFilter?.Value switch
    {
        TicketScope.House when shell.SelectedHouse is not null => $"Заявки за {TicketYear} год · {shell.SelectedHouse.AddressLine}",
        TicketScope.Apartment when shell.SelectedHouse is not null => $"Заявки за {TicketYear} год · {shell.SelectedHouse.AddressLine}, кв. {ScopeApartment}",
        _ => $"Заявки за {TicketYear} год · все дома"
    };


    public House? TicketHouse => SelectedTicket is null ? null : shell.Houses.FirstOrDefault(h => h.Id == SelectedTicket.HouseId);
    public Subscriber? TicketSubscriber => SelectedTicket?.Subscriber;
    public bool HasTicketSubscriber => TicketSubscriber is not null;

    public string TicketHeader => SelectedTicket is null ? "Заявка не выбрана"
        : $"Заявка № {SelectedTicket.Number} от {SelectedTicket.Date:dd.MM.yyyy} {SelectedTicket.Time:HH\\:mm}";
    public string TicketAddress => SelectedTicket is null ? ""
        : $"{TicketHouse?.AddressLine ?? SelectedTicket.House?.AddressLine}, под. {SelectedTicket.Section}, кв. {SelectedTicket.Apartment}";
    public string TicketHomeowner => TicketHouse?.Organization is { } o ? $"{o.Name}, {o.Branch}" : "не указан";
    public string TicketZone => TicketHouse?.Zone ?? "";
    public string TicketKindText => SelectedTicket?.KindLabel ?? "";
    public string TicketStatusText => SelectedTicket?.Status switch
    {
        TicketStatus.Done => SelectedTicket.DoneDate is { } d ? $"выполнена {d:dd.MM.yyyy}" : "выполнена",
        TicketStatus.Deferred => "отложена",
        TicketStatus.InProgress => "в работе",
        TicketStatus.Accepted => "принята",
        _ => ""
    };
    public string TicketAcceptedBy => SelectedTicket?.AcceptedBy ?? "";
    public string TicketSubscriberName => TicketSubscriber?.FullName ?? "без лицевого счёта";
    public string TicketAccount => TicketSubscriber?.Account ?? "—";
    public string TicketContract => TicketSubscriber switch
    {
        null => "—",
        { ContractDate: null } => "договора нет",
        { ContractTerminated: { } end } s => $"{ContractNo(s)}от {s.ContractDate:dd.MM.yyyy} с {s.ContractParty}, расторгнут {end:dd.MM.yyyy}",
        var s => $"{ContractNo(s)}от {s.ContractDate:dd.MM.yyyy} с {s.ContractParty}"
    };
    public string TicketLastPayment => TicketSubscriber?.LastPaymentDate is { } d
        ? $"{d:dd.MM.yyyy} ({PaymentAge.AgoText(d, shell.Today)})"
        : "нет";
    public bool IsTicketPaymentLate => TicketSubscriber is { } s && PaymentAge.IsLate(s, shell.Today, shell.PaymentWarningMonths);

    private static string ContractNo(Subscriber s) => string.IsNullOrWhiteSpace(s.ContractNumber) ? "" : $"№ {s.ContractNumber} ";
    public string TicketNotes => TicketSubscriber?.Notes is { Length: > 0 } n ? n : "—";

    public bool CanAcceptTickets => shell.User is not null && RoleCatalog.Can(shell.User.Role, AppModule.Disp, out var level) && level == RoleCatalog.AccessLevel.Edit;

    public bool CanEditSelectedTicket => shell.User is not null && SelectedTicket is not null && CanAcceptTickets &&
                                         RoleCatalog.CanEditTicket(shell.User.Role, shell.User.Id, SelectedTicket);

    public string TicketRightsHint => shell.User is null || SelectedTicket is null ? ""
        : CanEditSelectedTicket ? ""
        : !CanAcceptTickets ? $"Роль «{RoleCatalog.Title(shell.User.Role)}» смотрит заявки без правки."
        : $"Заявку принял(а) {SelectedTicket.AcceptedBy}: пользователь АДС правит только свои заявки, чужую меняет начальник АДС.";

    public bool HasTicketRightsHint => TicketRightsHint.Length > 0;


    public string HouseHomeowner => shell.SelectedHouse?.Organization is { } o ? $"{o.Name}, {o.Branch}" : "не указан";
    public string HouseHomeownerPhones => shell.SelectedHouse?.Organization?.Phones ?? "";
    public string HouseOpenTickets => shell.SelectedHouse is null ? ""
        : shell.AllTickets.Count(t => t.HouseId == shell.SelectedHouse.Id && t.Status != TicketStatus.Done).ToString();

    private string ScopeApartment => shell.SelectedSubscriber?.HouseId == shell.SelectedHouse?.Id && shell.SelectedSubscriber is not null
        ? shell.SelectedSubscriber.Apartment
        : shell.FindApartment.Trim();


    internal void LoadTicketYears()
    {
        EnsureTicketFilter();
        var years = TicketJournal.Years(shell.AllTickets, shell.Today);
        TicketYears.Clear();
        foreach (var y in years) TicketYears.Add(y);
        OnPropertyChanged(nameof(TicketYear));
    }

    private TicketFilter CurrentTicketFilter()
    {
        var filter = new TicketFilter
        {
            From = TicketFrom is { } f ? DateOnly.FromDateTime(f) : null,
            To = TicketTo is { } t ? DateOnly.FromDateTime(t) : null,
            Kind = KindFilter?.Value,
            Status = StatusFilter?.Value,
            OnlyOpen = OnlyOpenTickets,
            Text = TicketSearch
        };
        return ScopeFilter?.Value switch
        {
            TicketScope.House when shell.SelectedHouse is not null => filter with { HouseId = shell.SelectedHouse.Id },
            TicketScope.Apartment when shell.SelectedHouse is not null =>
                filter with { HouseId = shell.SelectedHouse.Id, Apartment = ScopeApartment },
            _ => filter
        };
    }

    internal void RefreshTickets()
    {
        EnsureTicketFilter();
        if (_holdTicketRefresh) return;
        var keep = SelectedTicket?.Id;
        Tickets.Clear();
        foreach (var t in shell.AllTickets.Apply(CurrentTicketFilter()).Newest())
            Tickets.Add(t);
        if (keep is not null && SelectedTicket is null)
            SelectedTicket = Tickets.FirstOrDefault(t => t.Id == keep);

        RefreshTicketMonths();
        OnPropertyChanged(nameof(TicketCountText));
        OnPropertyChanged(nameof(TicketPeriodText));
        OnPropertyChanged(nameof(TicketMonthsCaption));
    }

    private void RefreshTicketMonths()
    {
        // Помесячная сводка — за выбранный год, по тому же дому/квартире и виду, без отбора по датам.
        var filter = CurrentTicketFilter() with { From = null, To = null, Status = null, OnlyOpen = false, Text = null };
        var stats = TicketJournal.ByMonth(shell.AllTickets.Apply(filter), TicketYear);
        var from = TicketFrom is { } f ? DateOnly.FromDateTime(f) : (DateOnly?)null;
        var to = TicketTo is { } t ? DateOnly.FromDateTime(t) : (DateOnly?)null;
        TicketMonths.Clear();
        foreach (var m in stats)
        {
            var month = TicketFilter.ForMonth(TicketYear, m.Month);
            var selected = from == month.From && to == month.To;
            TicketMonths.Add(new TicketMonthRow(shell.Months[m.Month - 1], m, selected, SelectTicketMonthCommand, m.Month));
        }
    }

    internal void NotifyTicketPanel()
    {
        OnPropertyChanged(nameof(HouseHomeowner));
        OnPropertyChanged(nameof(HouseHomeownerPhones));
        OnPropertyChanged(nameof(HouseOpenTickets));
        OnPropertyChanged(nameof(CanAcceptTickets));
        OnPropertyChanged(nameof(TicketMonthsCaption));
    }

    internal void BindTicket()
    {
        TicketFault = SelectedTicket?.Fault ?? "";
        TicketWork = SelectedTicket?.WorkDone ?? "";
        TicketMechanic = SelectedTicket?.Mechanic ?? "";
        OnPropertyChanged(nameof(TicketHouse));
        OnPropertyChanged(nameof(TicketSubscriber));
        OnPropertyChanged(nameof(HasTicketSubscriber));
        OnPropertyChanged(nameof(TicketHeader));
        OnPropertyChanged(nameof(TicketAddress));
        OnPropertyChanged(nameof(TicketHomeowner));
        OnPropertyChanged(nameof(TicketZone));
        OnPropertyChanged(nameof(TicketKindText));
        OnPropertyChanged(nameof(TicketStatusText));
        OnPropertyChanged(nameof(TicketAcceptedBy));
        OnPropertyChanged(nameof(TicketSubscriberName));
        OnPropertyChanged(nameof(TicketAccount));
        OnPropertyChanged(nameof(TicketContract));
        OnPropertyChanged(nameof(TicketLastPayment));
        OnPropertyChanged(nameof(IsTicketPaymentLate));
        OnPropertyChanged(nameof(TicketNotes));
        OnPropertyChanged(nameof(CanEditSelectedTicket));
        OnPropertyChanged(nameof(CanAcceptTickets));
        OnPropertyChanged(nameof(TicketRightsHint));
        OnPropertyChanged(nameof(HasTicketRightsHint));
        shell.NotifyWindowTitle();
    }

    private void SetPeriod(DateOnly? from, DateOnly? to)
    {
        // Обе даты меняем разом, чтобы журнал пересчитался один раз.
        _holdTicketRefresh = true;
        TicketFrom = from?.ToDateTime(TimeOnly.MinValue);
        TicketTo = to?.ToDateTime(TimeOnly.MinValue);
        _holdTicketRefresh = false;
        RefreshTickets();
    }


    [RelayCommand]
    private void PeriodToday() => SetPeriod(shell.Today, shell.Today);

    [RelayCommand]
    private void PeriodMonth() => SelectTicketMonth(shell.Today.Year == TicketYear ? shell.Today.Month : 1);

    [RelayCommand]
    private void PeriodYear() => SetPeriod(new DateOnly(TicketYear, 1, 1), new DateOnly(TicketYear, 12, 31));

    [RelayCommand]
    private void PeriodAll() => SetPeriod(null, null);

    [RelayCommand]
    private void SelectTicketMonth(int month)
    {
        var f = TicketFilter.ForMonth(TicketYear, month);
        SetPeriod(f.From, f.To);
    }

    [RelayCommand]
    private void PrintWorkOrder()
    {
        if (SelectedTicket is not { } t) { shell.Status = "Выберите заявку в журнале."; return; }
        var address = TicketAddress;
        shell.OpenPdf(MainViewModel.PrintPath("наряд-заказ", t.Number.ToString()), $"наряд-заказ № {t.Number}", path => PdfForms.WorkOrder(t, address, path));
    }

    [RelayCommand]
    private async Task OpenTicketSubscriber()
    {
        if (TicketSubscriber is not { } s) { shell.Status = "У заявки нет лицевого счёта."; return; }
        await SelectSubscriberOf(s);
        shell.GoCommand.Execute(AppModule.Subscriber);
    }

    [RelayCommand]
    private async Task OpenTicketContract()
    {
        if (TicketSubscriber is not { } s) { shell.Status = "У заявки нет лицевого счёта, договора не найти."; return; }
        await SelectSubscriberOf(s);
        shell.OpenContractCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenTicketHouse()
    {
        if (TicketHouse is not { } h) return;
        shell.SelectedStreet = shell.Streets.FirstOrDefault(x => x.Id == h.StreetId);
        shell.SelectedHouse = shell.HousesOnStreet.FirstOrDefault(x => x.Id == h.Id);
        shell.GoCommand.Execute(AppModule.Anten);
    }

    [RelayCommand]
    private void EditTicket()
    {
        if (SelectedTicket is null) { shell.Status = "Выберите заявку в журнале."; return; }
        if (!CanEditSelectedTicket) { shell.Status = TicketRightsHint; return; }
        shell.GoCommand.Execute(AppModule.Ticket);
    }

    private async Task SelectSubscriberOf(Subscriber s)
    {
        var keep = SelectedTicket;
        await shell.ShowSubscriberAsync(s.Id, s.HouseId);
        SelectedTicket = keep;
    }

    [RelayCommand]
    private async Task AcceptTicket()
    {
        if (shell.SelectedHouse is null || shell.User is null) return;
        if (!CanAcceptTickets) { shell.Status = $"Роль «{RoleCatalog.Title(shell.User.Role)}» не принимает заявки."; return; }
        var subscriber = shell.SelectedSubscriber?.HouseId == shell.SelectedHouse.Id ? shell.SelectedSubscriber : null;
        var request = new CreateTicketRequest(
            shell.SelectedHouse.Id,
            string.IsNullOrWhiteSpace(NewTicketFault) ? "Принята с рабочего места диспетчера" : NewTicketFault.Trim(),
            (NewTicketKind?.Value ?? TicketKind.Antenna).ToString(),
            subscriber?.Id,
            subscriber?.Section ?? shell.FindSection,
            subscriber?.Apartment ?? shell.FindApartment,
            subscriber?.Phone ?? "");
        await shell.Save(async () =>
        {
            var number = await shell.Store.AcceptTicketAsync(request, shell.User);
            await shell.ReloadAsync();
            NewTicketFault = "";
            SelectedTicket = Tickets.FirstOrDefault(t => t.Number == number);
            shell.Status = $"Принята заявка № {number}.";
            shell.Module = AppModule.Disp;
        });
    }

    [RelayCommand]
    private async Task SaveTicket()
    {
        if (SelectedTicket is null) return;
        if (!CanEditSelectedTicket) { shell.Status = TicketRightsHint; return; }
        var (id, number) = (SelectedTicket.Id, SelectedTicket.Number);
        await shell.Save(async () =>
        {
            await shell.Store.SaveTicketAsync(id, TicketFault.Trim(), TicketMechanic.Trim(), TicketWork);
            await shell.ReloadAsync();
            SelectedTicket = Tickets.FirstOrDefault(x => x.Number == number) ?? shell.AllTickets.FirstOrDefault(x => x.Number == number);
            shell.Status = $"Заявка № {number} записана.";
            shell.Module = AppModule.Disp;
        });
    }
}

/// <summary>Клетка помесячной сводки: «Авг · 6 / 4».</summary>
public sealed class TicketMonthRow(string name, TicketMonthStat stat, bool selected, ICommand select, int month)
{
    public string Name { get; } = name;
    public int Month { get; } = month;
    public string Counts { get; } = stat.Accepted == 0 ? "—" : $"{stat.Accepted} / {stat.Done}";
    public bool HasOpen { get; } = stat.Open > 0;
    public bool IsSelected { get; } = selected;
    public string Tip { get; } = $"Принято {stat.Accepted}, выполнено {stat.Done}, не выполнено {stat.Open}. Нажмите — журнал за месяц.";
    public ICommand Select { get; } = select;
}
