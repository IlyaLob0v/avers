using System.Collections.ObjectModel;
using Avers.Desktop.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>Навигация между модулями: кнопки панели, меню «Модули», ярлыки рабочего стола.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Модули, доступные вошедшему пользователю, в порядке <see cref="NavCatalog"/>.</summary>
    public ObservableCollection<NavItem> Navigation { get; } = [];

    public ObservableCollection<NavGroup> NavigationGroups { get; } = [];

    /// <summary>Пройденные экраны для кнопки «Назад»; сбрасываются при выходе.</summary>
    private readonly Stack<NavigationState> _history = new();

    private sealed record NavigationState(
        AppModule Module, int? StreetId, int? HouseId, int? SubscriberId,
        bool IsHouseList, string HouseFilter, int? HouseResultId,
        SearchMode SearchMode, string FindQuery, string FindStreet, string FindHouse,
        string FindApartment, string FindSection,
        IReadOnlyList<SubscriberMatch> SearchMatches, SubscriberMatch? SelectedSearchMatch,
        TicketsViewModel.JournalNavigationState Journal);

    private NavigationState CaptureNavigation(AppModule module) => new(
        module, SelectedStreet?.Id, SelectedHouse?.Id, SelectedSubscriber?.Id,
        IsHouseList, HouseFilter, SelectedHouseResult?.Id,
        SearchMode, FindQuery, FindStreet, FindHouse, FindApartment, FindSection,
        SearchMatches, SelectedSearchMatch, TicketsScreen.CaptureNavigation());

    public string ModuleTitle => Module == AppModule.Desktop ? "Рабочий стол" : NavCatalog.Owner(Module)?.Title ?? "";

    public bool IsOnDesktop => Module == AppModule.Desktop;

    public string UserLine => User is null ? "" : $"{User.DisplayName} · {UserRoleTitle}";

    public string UserRoleTitle => User is null ? "" : RoleCatalog.Title(User.Role);

    private void Remember(AppModule current)
    {
        _history.Push(CaptureNavigation(current));
        BackCommand.NotifyCanExecuteChanged();
    }

    private bool CanBack() => _history.Count > 0;

    [RelayCommand(CanExecute = nameof(CanBack))]
    private async Task Back()
    {
        if (!TryLeaveEdits(Back)) return;
        var user = User;
        if (user is null) return;
        // После записи заявки программа сама возвращается в журнал — такой экран пропускаем.
        while (_history.TryPop(out var previous))
        {
            if (previous == CaptureNavigation(Module)) continue;
            if (!CanEnterModule(previous.Module)) continue;
            var house = _houses.FirstOrDefault(h => h.Id == previous.HouseId);
            _holdRefresh = true;
            SelectedStreet = Streets.FirstOrDefault(s => s.Id == (house?.StreetId ?? previous.StreetId));
            RefreshHouses();
            SelectedHouse = house;
            SelectedSubscriber = null;
            _holdRefresh = false;
            IsHouseList = previous.IsHouseList;
            HouseFilter = previous.HouseFilter;
            SearchMode = previous.SearchMode;
            FindQuery = previous.FindQuery;
            FindStreet = previous.FindStreet;
            FindHouse = previous.FindHouse;
            FindApartment = previous.FindApartment;
            FindSection = previous.FindSection;
            SearchMatches = previous.SearchMatches;
            SelectedSearchMatch = previous.SelectedSearchMatch;
            Module = previous.Module;
            await ShowHouseAsync(previous.SubscriberId);
            if (User != user || Module != previous.Module || SelectedHouse?.Id != previous.HouseId
                || SelectedSubscriber?.Id != previous.SubscriberId) return;
            await TicketsScreen.RestoreNavigationAsync(previous.Journal);
            if (User != user || Module != previous.Module || SelectedHouse?.Id != previous.HouseId
                || SelectedSubscriber?.Id != previous.SubscriberId) return;
            SelectedHouseResult = _houses.FirstOrDefault(h => h.Id == previous.HouseResultId);
            Status = WindowTitle;
            break;
        }
        BackCommand.NotifyCanExecuteChanged();
    }

    private void RebuildNavigation()
    {
        Navigation.Clear();
        NavigationGroups.Clear();
        if (User is not null)
        {
            foreach (var entry in NavCatalog.All.Where(e => NavCatalog.IsAvailable(User.Role, e)))
                Navigation.Add(new NavItem(entry, OpenModuleCommand));

            AddGroup("Абоненты и расчёты", [AppModule.Abon, AppModule.Contract, AppModule.PrDubl]);
            AddGroup("Дома и обслуживание", [AppModule.Disp, AppModule.Anten, AppModule.Domofo, AppModule.Ppr, AppModule.Reader]);
            AddGroup("Должники и отчёты", [AppModule.Legal, AppModule.Reports]);
            AddGroup("Справочники и управление", [AppModule.Phonebook, AppModule.Access, AppModule.ChangeLog]);
        }
        MarkCurrent();
    }

    private void AddGroup(string title, AppModule[] modules)
    {
        var items = modules.Select(module => Navigation.FirstOrDefault(item => item.Module == module))
            .OfType<NavItem>().ToArray();
        if (items.Length > 0) NavigationGroups.Add(new NavGroup(title, items));
    }

    private void MarkCurrent()
    {
        var owner = NavCatalog.Owner(Module)?.Module;
        foreach (var item in Navigation)
            item.IsCurrent = item.Module == owner;
    }
}
