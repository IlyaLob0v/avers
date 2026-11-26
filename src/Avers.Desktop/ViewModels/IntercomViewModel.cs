using System.Collections.ObjectModel;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.Printing;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Домофоны дома: панели подъездов (правит тот, кто правит паспорт дома), приём и закрытие заявок
/// по домофону (кто принимает заявки АДС), печать сводки по дому с невыполненными заявками.
/// </summary>
public sealed partial class IntercomViewModel(MainViewModel shell) : ObservableObject
{
    private int _requestVersion;
    private int? _loadedHouseId;
    private int? _panelHouseId;
    private IntercomPanel? _selectedIntercomPanel;
    private PanelSnapshot? _panelBaseline;
    private sealed record PanelSnapshot(int Entrance, string Type, string SwitchBlock, string Keys, string Power);
    /// <summary>Улицы, дома и радиомеханики — общие для всей программы.</summary>
    public MainViewModel Shell => shell;

    public ObservableCollection<IntercomTicket> IntercomTickets { get; } = [];

    [ObservableProperty] private IntercomPanel? _intercom;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CloseIntercomTicketCommand))]
    private IntercomTicket? _selectedIntercomTicket;
    [ObservableProperty] private bool _showClosedIntercomTickets;
    [ObservableProperty] private string _intercomEntrance = "1";
    [ObservableProperty] private string _intercomFault = "";
    [ObservableProperty] private Mechanic? _intercomMechanic;

    public bool CanEditIntercoms => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Anten, out var level) && level == RoleCatalog.AccessLevel.Edit;
    public bool CanAcceptIntercomTickets => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Disp, out var level) && level == RoleCatalog.AccessLevel.Edit;
    public bool HasPanelEditor => Intercom is not null;
    public bool HasNoPanels => shell.SelectedHouse is not null && Intercom is null;
    public IEnumerable<IntercomTicket> VisibleIntercomTickets => IntercomTickets.Where(ticket => ShowClosedIntercomTickets || !ticket.Done);
    public string IntercomTicketSummary => $"Открытых: {IntercomTickets.Count(ticket => !ticket.Done)} · всего: {IntercomTickets.Count}";
    internal bool HasPanelEdits => Intercom is { } panel && (_panelBaseline is null || CapturePanel(panel) != _panelBaseline);

    public Street? SelectedStreet
    {
        get => shell.SelectedStreet;
        set
        {
            // Перечитывание справочника временно сбрасывает выбор ComboBox в null.
            if (value is null || shell.SelectedStreet?.Id == value.Id) return;
            if (!shell.TryLeaveEdits(() => { SelectedStreet = value; return Task.CompletedTask; }))
            {
                OnPropertyChanged();
                return;
            }
            shell.SelectedStreet = shell.Streets.FirstOrDefault(street => street.Id == value?.Id);
            OnPropertyChanged();
        }
    }

    public House? SelectedHouse
    {
        get => shell.SelectedHouse;
        set
        {
            if (value is null || shell.SelectedHouse?.Id == value.Id) return;
            if (!shell.TryLeaveEdits(() => { SelectedHouse = value; return Task.CompletedTask; }))
            {
                OnPropertyChanged();
                return;
            }
            shell.SelectedHouse = shell.HousesOnStreet.FirstOrDefault(house => house.Id == value?.Id);
            OnPropertyChanged();
        }
    }

    /// <summary>Список хранит сохранённые панели, поля формы — отдельный черновик.</summary>
    public IntercomPanel? SelectedIntercomPanel
    {
        get => _selectedIntercomPanel;
        set
        {
            if (value is null || value.Id == _selectedIntercomPanel?.Id) return;
            if (!shell.TryLeaveEdits(() => { SelectedIntercomPanel = value; return Task.CompletedTask; }))
            {
                OnPropertyChanged();
                return;
            }
            BindPanel(shell.SelectedHouse?.Intercoms.FirstOrDefault(panel => panel.Id == value.Id));
        }
    }

    /// <summary>Сменился пользователь: права на панели и заявки.</summary>
    internal void RefreshRights()
    {
        OnPropertyChanged(nameof(CanEditIntercoms));
        OnPropertyChanged(nameof(CanAcceptIntercomTickets));
        NewIntercomPanelCommand.NotifyCanExecuteChanged();
        SaveIntercomPanelCommand.NotifyCanExecuteChanged();
        CloseIntercomTicketCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Выбран другой дом или перечитаны данные: заявки дома (только при смене дома) и панель того же подъезда.</summary>
    internal void Refresh(bool houseChanged)
    {
        var house = shell.SelectedHouse;
        if (houseChanged)
        {
            ClearTickets();
            if (shell.Module == AppModule.Domofo) _ = LoadTicketsAsync();
        }
        OnPropertyChanged(nameof(SelectedStreet));
        OnPropertyChanged(nameof(SelectedHouse));
        NewIntercomPanelCommand.NotifyCanExecuteChanged();
        // Перечитывание других сведений дома не должно стирать ещё не сохранённую панель.
        if (house?.Id == _panelHouseId && HasPanelEdits)
        {
            _selectedIntercomPanel = house?.Intercoms.FirstOrDefault(panel => panel.Id == Intercom!.Id);
            OnPropertyChanged(nameof(SelectedIntercomPanel));
            return;
        }
        BindPanel(house?.Intercoms.FirstOrDefault(i => i.Entrance == Intercom?.Entrance) ?? house?.Intercoms.FirstOrDefault());
    }

    internal void ClearTickets()
    {
        ++_requestVersion;
        _loadedHouseId = null;
        IntercomTickets.Clear();
        SelectedIntercomTicket = null;
        NotifyTicketList();
    }

    internal async Task<bool> LoadTicketsAsync(bool force = false)
    {
        if (!shell.SeesTickets || shell.User is null || shell.SelectedHouse is not { } house)
        {
            ClearTickets();
            return true;
        }
        if (!force && _loadedHouseId == house.Id) return true;
        var version = ++_requestVersion;
        var keep = SelectedIntercomTicket?.Id;
        try
        {
            var rows = await shell.Store.LoadIntercomTicketsAsync(house.Id);
            if (version != _requestVersion || shell.SelectedHouse?.Id != house.Id) return false;
            _loadedHouseId = house.Id;
            IntercomTickets.Clear();
            foreach (var row in rows) IntercomTickets.Add(row);
            SelectedIntercomTicket = VisibleIntercomTickets.FirstOrDefault(t => t.Id == keep);
            NotifyTicketList();
            return true;
        }
        catch (StoreException e)
        {
            if (version == _requestVersion) shell.Status = e.Message;
            return false;
        }
    }

    partial void OnIntercomChanged(IntercomPanel? value)
    {
        if (value is not null)
            IntercomEntrance = value.Entrance.ToString();
        OnPropertyChanged(nameof(HasPanelEditor));
        OnPropertyChanged(nameof(HasNoPanels));
        SaveIntercomPanelCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowClosedIntercomTicketsChanged(bool value)
    {
        if (!value && SelectedIntercomTicket is { Done: true }) SelectedIntercomTicket = null;
        NotifyTicketList();
    }

    private void NotifyTicketList()
    {
        OnPropertyChanged(nameof(VisibleIntercomTickets));
        OnPropertyChanged(nameof(IntercomTicketSummary));
    }

    private static PanelSnapshot CapturePanel(IntercomPanel panel) => new(panel.Entrance, panel.PanelType, panel.SwitchBlock, panel.Keys, panel.Power);

    private void BindPanel(IntercomPanel? panel)
    {
        _panelHouseId = shell.SelectedHouse?.Id;
        _selectedIntercomPanel = panel;
        OnPropertyChanged(nameof(SelectedIntercomPanel));
        Intercom = panel is null ? null : new IntercomPanel
        {
            Id = panel.Id,
            HouseId = panel.HouseId,
            Entrance = panel.Entrance,
            PanelType = panel.PanelType,
            SwitchBlock = panel.SwitchBlock,
            Keys = panel.Keys,
            Power = panel.Power
        };
        _panelBaseline = Intercom is null ? null : CapturePanel(Intercom);
        OnPropertyChanged(nameof(HasPanelEditor));
        OnPropertyChanged(nameof(HasNoPanels));
        SaveIntercomPanelCommand.NotifyCanExecuteChanged();
    }

    internal void DiscardPanelEdits() => BindPanel(shell.SelectedHouse?.Intercoms.FirstOrDefault(panel => panel.Id == Intercom?.Id)
        ?? shell.SelectedHouse?.Intercoms.FirstOrDefault());

    private bool CanCreatePanel() => CanEditIntercoms && shell.SelectedHouse is not null;
    private bool CanSavePanel() => CanEditIntercoms && Intercom is { } panel && panel.HouseId == shell.SelectedHouse?.Id;
    private bool CanCloseTicket() => CanAcceptIntercomTickets && SelectedIntercomTicket is { Done: false } ticket
        && ticket.HouseId == shell.SelectedHouse?.Id;

    [RelayCommand(CanExecute = nameof(CanCreatePanel))]
    private void NewIntercomPanel()
    {
        if (!CanCreatePanel() || shell.SelectedHouse is not { } house) return;
        if (!shell.TryLeaveEdits(() => { NewIntercomPanel(); return Task.CompletedTask; })) return;
        BindPanel(null);
        Intercom = new IntercomPanel { HouseId = house.Id, Entrance = house.Intercoms.Select(i => i.Entrance).DefaultIfEmpty().Max() + 1 };
        shell.Status = "Новая панель: проверьте номер подъезда, заполните поля и нажмите «Сохранить панель».";
    }

    [RelayCommand(CanExecute = nameof(CanSavePanel))]
    private async Task SaveIntercomPanel() => await SavePanelEditsAsync();

    internal async Task<bool> SavePanelEditsAsync()
    {
        if (!CanSavePanel() || shell.SelectedHouse is not { } house || Intercom is not { } panel) return false;
        var saved = false;
        var user = shell.User;
        await shell.Save(async () =>
        {
            await shell.Store.SaveIntercomPanelAsync(house.Id, panel);
            _panelBaseline = CapturePanel(panel);
            await shell.ReloadAsync();
            if (shell.User != user || shell.SelectedHouse?.Id != house.Id) return;
            BindPanel(shell.SelectedHouse.Intercoms.FirstOrDefault(i => i.Entrance == panel.Entrance));
            shell.Status = $"Панель подъезда {panel.Entrance} сохранена.";
            saved = true;
        });
        return saved;
    }

    [RelayCommand]
    private async Task AcceptIntercomTicket()
    {
        if (shell.SelectedHouse is not { } house) return;
        if (!int.TryParse(IntercomEntrance, out var entrance))
        {
            shell.Status = "Подъезд — число.";
            return;
        }
        var ticket = new IntercomTicket { Entrance = entrance, Fault = IntercomFault, Mechanic = IntercomMechanic?.FullName ?? "" };
        await shell.Save(async () =>
        {
            await shell.Store.AddIntercomTicketAsync(house.Id, ticket);
            await shell.ReloadAsync();
            await LoadTicketsAsync(force: true);
            IntercomFault = "";
            shell.Status = $"Заявка по домофону принята: подъезд {entrance}.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanCloseTicket))]
    private async Task CloseIntercomTicket()
    {
        if (!CanCloseTicket() || SelectedIntercomTicket is not { Done: false } ticket) return;
        await shell.Save(async () =>
        {
            await shell.Store.CloseIntercomTicketAsync(ticket.Id);
            await shell.ReloadAsync();
            await LoadTicketsAsync(force: true);
            shell.Status = $"Заявка от {ticket.Date:dd.MM.yyyy} выполнена.";
        });
    }

    [RelayCommand]
    private async Task PrintIntercom()
    {
        if (shell.SelectedHouse is not { } house) return;
        if (!await LoadTicketsAsync(force: true) || shell.SelectedHouse?.Id != house.Id) return;
        var dir = AversPaths.FilesDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"домофон-{house.Id}.pdf");
        try
        {
            PdfForms.Intercom(house, IntercomTickets.Where(t => !t.Done), shell.Today, path);
            shell.OpenFile(path);
            shell.Status = $"Открыт PDF: домофоны {house.AddressLine}. Печать — из программы просмотра.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            shell.Status = $"Не удалось открыть PDF: {e.Message}";
        }
    }
}
