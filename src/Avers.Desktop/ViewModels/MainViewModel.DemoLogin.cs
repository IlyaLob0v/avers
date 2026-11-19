using CommunityToolkit.Mvvm.ComponentModel;

namespace Avers.Desktop.ViewModels;

public sealed record DemoAccount(string Login, string Title, string Description);

public sealed partial class MainViewModel
{
    public static IReadOnlyList<DemoAccount> DemoAccounts { get; } =
    [
        new("sokolova", "Диспетчер", "Приём обращений и работа со своими заявками. Абоненты и дома доступны для просмотра."),
        new("orlova", "Диспетчер · вторая смена", "Второй диспетчер для проверки совместной работы и просмотра чужих заявок."),
        new("orlov", "Начальник АДС", "Оплаты, все заявки и выгрузка для 1С. Паспорта домов доступны для просмотра."),
        new("engineer", "Главный инженер", "Паспорта домов, схемы, неисправности, графики ППР и справочники."),
        new("lawyer", "Юрист", "Должники, уведомления, судебные дела и документы для суда."),
        new("novikov", "Радиомеханик", "Просмотр домов, схем и карты."),
        new("admin", "Администратор", "Все разделы, сотрудники, права доступа и журналы действий.")
    ];

    public string ApplicationVersion => $"Аверс {typeof(MainViewModel).Assembly.GetName().Version?.ToString(3)}";

    public bool ShowDemoAccounts { get; set; }
    public bool ShowLocalAccounts => !IsShared && !ShowDemoAccounts;
    public bool EnterLoginName => IsShared && !ShowDemoAccounts;

    [ObservableProperty] private DemoAccount _selectedDemoAccount = DemoAccounts[0];

    partial void OnSelectedDemoAccountChanged(DemoAccount value)
    {
        Password = "";
        LoginError = null;
    }
}
