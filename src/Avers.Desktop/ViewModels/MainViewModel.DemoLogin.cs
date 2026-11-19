using CommunityToolkit.Mvvm.ComponentModel;

namespace Avers.Desktop.ViewModels;

public sealed record DemoAccount(string Login, string Title, string Description);

public sealed partial class MainViewModel
{
    public static IReadOnlyList<DemoAccount> DemoAccounts { get; } =
    [
        new("sokolova", "Диспетчер · sokolova", "Приём обращений и работа со своими заявками. Абоненты и дома доступны для просмотра."),
        new("orlova", "Диспетчер · orlova", "Второй диспетчер для проверки совместной работы и просмотра чужих заявок."),
        new("orlov", "Начальник АДС · orlov", "Оплаты, все заявки и выгрузка для 1С. Паспорта домов доступны для просмотра."),
        new("engineer", "Главный инженер · engineer", "Паспорта домов, схемы, неисправности, графики ППР и справочники."),
        new("lawyer", "Юрист · lawyer", "Должники, уведомления, судебные дела и документы для суда."),
        new("novikov", "Радиомеханик · novikov", "Просмотр домов, схем и карты."),
        new("admin", "Администратор · admin", "Все разделы, сотрудники, права доступа и журналы действий.")
    ];

    public bool ShowDemoAccounts { get; set; }
    public bool EnterLoginName => IsShared && !ShowDemoAccounts;

    [ObservableProperty] private DemoAccount _selectedDemoAccount = DemoAccounts[0];
}
