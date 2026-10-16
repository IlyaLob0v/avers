using System.Collections.ObjectModel;
using Avers.Domain;

namespace Avers.Desktop.ViewModels;

/// <summary>Навигация между модулями: кнопки панели, меню «Модули», ярлыки рабочего стола.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Модули, доступные вошедшему пользователю, в порядке <see cref="NavCatalog"/>.</summary>
    public ObservableCollection<NavItem> Navigation { get; } = [];

    public string ModuleTitle => Module == AppModule.Desktop ? "Рабочий стол" : NavCatalog.Owner(Module)?.Title ?? "";

    /// <summary>Как модуль назывался в прежнем Аверсе — для строки состояния.</summary>
    public string ModuleLegacyName => NavCatalog.Owner(Module)?.LegacyName ?? "Аверс";

    public bool IsOnDesktop => Module == AppModule.Desktop;

    public string UserLine => User is null ? "" : $"{User.DisplayName} · {UserRoleTitle}";

    public string UserRoleTitle => User is null ? "" : RoleCatalog.Title(User.Role);

    private void RebuildNavigation()
    {
        Navigation.Clear();
        if (User is not null)
        {
            foreach (var entry in NavCatalog.All.Where(e => NavCatalog.IsAvailable(User.Role, e)))
                Navigation.Add(new NavItem(entry, GoCommand));
        }
        MarkCurrent();
    }

    private void MarkCurrent()
    {
        var owner = NavCatalog.Owner(Module)?.Module;
        foreach (var item in Navigation)
            item.IsCurrent = item.Module == owner;
    }
}
