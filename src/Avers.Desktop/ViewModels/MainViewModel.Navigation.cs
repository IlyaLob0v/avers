using System.Collections.ObjectModel;
using System.ComponentModel;
using Avers.Domain;

namespace Avers.Desktop.ViewModels;

// Навигация между модулями: кнопки панели, меню «Модули», ярлыки рабочего стола.
// Отдельным файлом, чтобы правки экранов в MainViewModel.cs не пересекались с правками оболочки.
public sealed partial class MainViewModel
{
    private ObservableCollection<NavItem>? _navigation;

    /// <summary>Модули, доступные вошедшему пользователю, в порядке <see cref="NavCatalog"/>.</summary>
    public ObservableCollection<NavItem> Navigation
    {
        get
        {
            if (_navigation is null)
            {
                _navigation = [];
                PropertyChanged += OnNavigationSourceChanged;
                RebuildNavigation();
            }
            return _navigation;
        }
    }

    /// <summary>Название текущего модуля для заголовка окна.</summary>
    public string ModuleTitle => Current(m => m == AppModule.Desktop ? "Рабочий стол" : NavCatalog.Owner(m)?.Title ?? "");

    /// <summary>Как модуль назывался в прежнем Аверсе — для строки состояния.</summary>
    public string ModuleLegacyName => Current(m => NavCatalog.Owner(m)?.LegacyName ?? "Аверс");

    public bool IsOnDesktop => Current(m => m == AppModule.Desktop);

    public string UserLine => User is null ? "" : $"{User.DisplayName} · {RoleCatalog.Title(User.Role)}";

    // Первое обращение к свойствам навигации включает подписку на смену модуля и пользователя.
    private T Current<T>(Func<AppModule, T> read)
    {
        _ = Navigation;
        return read(Module);
    }

    private void OnNavigationSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(User))
        {
            RebuildNavigation();
            OnPropertyChanged(nameof(UserLine));
        }
        else if (e.PropertyName == nameof(Module))
        {
            MarkCurrent();
            OnPropertyChanged(nameof(ModuleTitle));
            OnPropertyChanged(nameof(ModuleLegacyName));
            OnPropertyChanged(nameof(IsOnDesktop));
        }
    }

    private void RebuildNavigation()
    {
        _navigation!.Clear();
        if (User is not null)
        {
            foreach (var entry in NavCatalog.All.Where(e => NavCatalog.IsAvailable(User.Role, e)))
                _navigation.Add(new NavItem(entry, GoCommand));
        }
        MarkCurrent();
    }

    private void MarkCurrent()
    {
        var owner = NavCatalog.Owner(Module)?.Module;
        foreach (var item in _navigation!)
            item.IsCurrent = item.Module == owner;
    }
}
