using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty] private bool _isHouseList = true;
    [ObservableProperty] private string _houseFilter = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(OpenHouseCommand))]
    private House? _selectedHouseResult;

    public IReadOnlyList<House> FilteredHouses
    {
        get
        {
            var words = HouseFilter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return _houses.Where(house => words.All(word => house.AddressLine.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(house => house.Street.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(house => house.Number, NaturalOrder.Instance).ToArray();
        }
    }

    public bool CanOpenSubscriberHouse => SelectedSubscriber is not null && CanEnterModule(AppModule.Anten);

    private bool CanEnterModule(AppModule module) => User is not null
        && (NavCatalog.Owner(module) is not { } owner || NavCatalog.IsAvailable(User.Role, owner));

    [RelayCommand]
    private void OpenModule(AppModule module)
    {
        if (!CanEnterModule(module)) { Go(module); return; }
        if (!TryLeaveEdits(() => { OpenModule(module); return Task.CompletedTask; })) return;
        if (module == Module && (module == AppModule.Anten && !IsHouseList
            || module == AppModule.Abon && SelectedSubscriber is not null))
            Remember(Module);
        Go(module);
        if (module == AppModule.Anten)
        {
            IsHouseList = true;
            SelectedHouseResult = null;
        }
        if (module == AppModule.Abon) ClearSubscriberSelection();
        Status = WindowTitle;
    }

    private bool CanOpenHouse(House? house) => (house ?? SelectedHouseResult) is not null && CanEnterModule(AppModule.Anten);

    [RelayCommand(CanExecute = nameof(CanOpenHouse))]
    private async Task OpenHouse(House? house)
    {
        house ??= SelectedHouseResult;
        if (house is null || !CanEnterModule(AppModule.Anten)) return;
        if (!TryLeaveEdits(() => OpenHouse(house))) return;
        var user = User;
        house = _houses.FirstOrDefault(candidate => candidate.Id == house.Id);
        if (house is null) return;
        var subscriberId = SelectedSubscriber?.HouseId == house.Id ? SelectedSubscriber.Id : (int?)null;
        if (Module == AppModule.Anten) Remember(Module);
        Go(AppModule.Anten);
        _holdRefresh = true;
        SelectedStreet = Streets.FirstOrDefault(street => street.Id == house.StreetId);
        RefreshHouses();
        SelectedHouse = house;
        _holdRefresh = false;
        SelectedHouseResult = house;
        IsHouseList = false;
        await ShowHouseAsync(subscriberId);
        if (User != user || Module != AppModule.Anten || SelectedHouse?.Id != house.Id) return;
        Status = WindowTitle;
    }

    [RelayCommand]
    private void ShowHouseList() => OpenModule(AppModule.Anten);

    [RelayCommand(CanExecute = nameof(CanOpenSubscriberHouse))]
    private async Task OpenSubscriberHouse()
    {
        if (!CanOpenSubscriberHouse) return;
        await OpenHouse(_houses.FirstOrDefault(house => house.Id == SelectedSubscriber!.HouseId));
    }

    internal async Task OpenSubscriberAsync(Subscriber subscriber, AppModule module = AppModule.Subscriber)
    {
        if (!CanEnterModule(module)) { Go(module); return; }
        if (!TryLeaveEdits(() => OpenSubscriberAsync(subscriber, module))) return;
        var user = User;
        if (Module == module && SelectedSubscriber?.Id != subscriber.Id) Remember(Module);
        var ticket = TicketsScreen.SelectedTicket;
        Go(module);
        await ShowSubscriberAsync(subscriber.Id, subscriber.HouseId);
        if (User != user || Module != module || SelectedSubscriber?.Id != subscriber.Id
            || SelectedHouse?.Id != subscriber.HouseId) return;
        TicketsScreen.SelectedTicket = ticket;
        if (module == AppModule.Contract)
            ContractsScreen.SelectedContractRow = ContractsScreen.ContractRows.FirstOrDefault(row => row.Subscriber.Id == subscriber.Id);
    }

    [RelayCommand]
    private void ShowSubscriberSearch() => OpenModule(AppModule.Abon);

    private void ClearSubscriberSelection()
    {
        ++_houseSubscribersVersion;
        IsLoadingSubscriber = false;
        SelectedSubscriber = null;
        SearchMatches = [];
        SelectedSearchMatch = null;
    }

    private void PrepareContextualNavigation(AppModule module)
    {
        if (module == AppModule.Anten) IsHouseList = false;
    }

    private void NotifyWorkflowContextChanged()
    {
        OnPropertyChanged(nameof(FilteredHouses));
        OnPropertyChanged(nameof(CanOpenSubscriberHouse));
        OpenHouseCommand.NotifyCanExecuteChanged();
        OpenSubscriberHouseCommand.NotifyCanExecuteChanged();
    }

    private void ResetWorkflows()
    {
        ClearSubscriberSelection();
        IsHouseList = true;
        HouseFilter = "";
        SelectedHouseResult = null;
        SearchMode = SearchMode.Address;
        FindQuery = FindStreet = FindHouse = FindApartment = FindSection = "";
    }

    partial void OnHouseFilterChanged(string value)
    {
        SelectedHouseResult = null;
        OnPropertyChanged(nameof(FilteredHouses));
    }

    partial void OnIsHouseListChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));
}
