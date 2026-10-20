using Avers.Desktop.Printing;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Домофоны дома: панели подъездов (правит тот, кто правит паспорт дома), приём и закрытие заявок
/// по домофону (кто принимает заявки АДС), печать сводки по дому с невыполненными заявками.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Выбор в списке подъездов; новая, ещё не сохранённая панель в списке не стоит.</summary>
    [ObservableProperty] private IntercomPanel? _selectedIntercomPanel;
    [ObservableProperty] private IntercomTicket? _selectedIntercomTicket;
    [ObservableProperty] private string _intercomEntrance = "1";
    [ObservableProperty] private string _intercomFault = "";
    [ObservableProperty] private Mechanic? _intercomMechanic;

    public bool CanEditIntercoms => User is { } u && RoleCatalog.Can(u.Role, AppModule.Anten, out var level) && level == RoleCatalog.AccessLevel.Edit;
    public bool CanAcceptIntercomTickets => User is { } u && RoleCatalog.Can(u.Role, AppModule.Disp, out var level) && level == RoleCatalog.AccessLevel.Edit;

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
        if (SelectedHouse is not { } house) return;
        Intercom = new IntercomPanel { HouseId = house.Id, Entrance = house.Intercoms.Select(i => i.Entrance).DefaultIfEmpty().Max() + 1 };
        Status = "Новая панель: проверьте номер подъезда, заполните поля и нажмите «Сохранить панель».";
    }

    [RelayCommand]
    private async Task SaveIntercomPanel()
    {
        if (SelectedHouse is not { } house || Intercom is not { } panel) return;
        await Save(async () =>
        {
            await _store.SaveIntercomPanelAsync(house.Id, panel);
            await ReloadAsync();
            Intercom = SelectedHouse?.Intercoms.FirstOrDefault(i => i.Entrance == panel.Entrance);
            Status = $"Панель подъезда {panel.Entrance} сохранена.";
        });
    }

    [RelayCommand]
    private async Task AcceptIntercomTicket()
    {
        if (SelectedHouse is not { } house) return;
        if (!int.TryParse(IntercomEntrance, out var entrance))
        {
            Status = "Подъезд — число.";
            return;
        }
        var ticket = new IntercomTicket { Entrance = entrance, Fault = IntercomFault, Mechanic = IntercomMechanic?.FullName ?? "" };
        await Save(async () =>
        {
            await _store.AddIntercomTicketAsync(house.Id, ticket);
            await ReloadAsync();
            IntercomFault = "";
            Status = $"Заявка по домофону принята: подъезд {entrance}.";
        });
    }

    [RelayCommand]
    private async Task CloseIntercomTicket()
    {
        if (SelectedIntercomTicket is not { Done: false } ticket) return;
        await Save(async () =>
        {
            await _store.CloseIntercomTicketAsync(ticket.Id);
            await ReloadAsync();
            Status = $"Заявка от {ticket.Date:dd.MM.yyyy} выполнена.";
        });
    }

    [RelayCommand]
    private void PrintIntercom()
    {
        if (SelectedHouse is not { } house) return;
        var dir = Path.Combine(Path.GetTempPath(), "Avers");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"домофон-{house.Id}.pdf");
        try
        {
            PdfForms.Intercom(house, IntercomTickets.Where(t => !t.Done), _today, path);
            OpenFile(path);
            Status = $"Открыт PDF: домофоны {house.AddressLine}. Печать — из программы просмотра.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Status = $"Не удалось открыть PDF: {e.Message}";
        }
    }
}
