using System.Collections.ObjectModel;
using Avers.Data;
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
    /// <summary>Улицы, дома и радиомеханики — общие для всей программы.</summary>
    public MainViewModel Shell => shell;

    public ObservableCollection<IntercomTicket> IntercomTickets { get; } = [];

    [ObservableProperty] private IntercomPanel? _intercom;
    /// <summary>Выбор в списке подъездов; новая, ещё не сохранённая панель в списке не стоит.</summary>
    [ObservableProperty] private IntercomPanel? _selectedIntercomPanel;
    [ObservableProperty] private IntercomTicket? _selectedIntercomTicket;
    [ObservableProperty] private string _intercomEntrance = "1";
    [ObservableProperty] private string _intercomFault = "";
    [ObservableProperty] private Mechanic? _intercomMechanic;

    public bool CanEditIntercoms => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Anten, out var level) && level == RoleCatalog.AccessLevel.Edit;
    public bool CanAcceptIntercomTickets => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Disp, out var level) && level == RoleCatalog.AccessLevel.Edit;

    /// <summary>Сменился пользователь: права на панели и заявки.</summary>
    internal void RefreshRights()
    {
        OnPropertyChanged(nameof(CanEditIntercoms));
        OnPropertyChanged(nameof(CanAcceptIntercomTickets));
    }

    /// <summary>Выбран другой дом или перечитаны данные: заявки дома (только при смене дома) и панель того же подъезда.</summary>
    internal void Refresh(bool houseChanged)
    {
        var house = shell.SelectedHouse;
        if (houseChanged)
        {
            IntercomTickets.Clear();
            if (house is not null)
                foreach (var t in shell.AllIntercomTickets.Where(x => x.HouseId == house.Id))
                    IntercomTickets.Add(t);
        }
        Intercom = house?.Intercoms.FirstOrDefault(i => i.Entrance == Intercom?.Entrance) ?? house?.Intercoms.FirstOrDefault();
        OnPropertyChanged(nameof(Intercom));
    }

    partial void OnIntercomChanged(IntercomPanel? value)
    {
        if (value is not null)
            IntercomEntrance = value.Entrance.ToString();
        SelectedIntercomPanel = value is { Id: > 0 } ? value : null;
    }

    partial void OnSelectedIntercomPanelChanged(IntercomPanel? value)
    {
        if (value is not null)
            Intercom = value;
    }

    [RelayCommand]
    private void NewIntercomPanel()
    {
        if (shell.SelectedHouse is not { } house) return;
        Intercom = new IntercomPanel { HouseId = house.Id, Entrance = house.Intercoms.Select(i => i.Entrance).DefaultIfEmpty().Max() + 1 };
        shell.Status = "Новая панель: проверьте номер подъезда, заполните поля и нажмите «Сохранить панель».";
    }

    [RelayCommand]
    private async Task SaveIntercomPanel()
    {
        if (shell.SelectedHouse is not { } house || Intercom is not { } panel) return;
        await shell.Save(async () =>
        {
            await shell.Store.SaveIntercomPanelAsync(house.Id, panel);
            await shell.ReloadAsync();
            Intercom = shell.SelectedHouse?.Intercoms.FirstOrDefault(i => i.Entrance == panel.Entrance);
            shell.Status = $"Панель подъезда {panel.Entrance} сохранена.";
        });
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
            IntercomFault = "";
            shell.Status = $"Заявка по домофону принята: подъезд {entrance}.";
        });
    }

    [RelayCommand]
    private async Task CloseIntercomTicket()
    {
        if (SelectedIntercomTicket is not { Done: false } ticket) return;
        await shell.Save(async () =>
        {
            await shell.Store.CloseIntercomTicketAsync(ticket.Id);
            await shell.ReloadAsync();
            shell.Status = $"Заявка от {ticket.Date:dd.MM.yyyy} выполнена.";
        });
    }

    [RelayCommand]
    private void PrintIntercom()
    {
        if (shell.SelectedHouse is not { } house) return;
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
