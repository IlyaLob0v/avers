using System.Windows.Input;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avers.Desktop.ViewModels;

/// <summary>Модуль в панели навигации, меню «Модули» и на рабочем столе.</summary>
public sealed record NavEntry(
    AppModule Module,
    string Title,
    string ShortTitle,
    string Glyph,
    string LegacyName,
    string Hint,
    string Summary,
    AppModule AccessModule,
    AppModule[] Children);

/// <summary>
/// Перечень модулей единого рабочего места. Порядок — порядок кнопок.
/// Summary — строка под названием на плитке рабочего стола, Hint — подсказка у кнопки модуля.
/// AccessModule — по какому праву из <see cref="RoleCatalog"/> модуль показывается роли.
/// Children — окна, открытые из модуля: пока они на экране, кнопка модуля подсвечена.
/// </summary>
public static class NavCatalog
{
    public static IReadOnlyList<NavEntry> All { get; } =
    [
        new(AppModule.Abon, "Абоненты и оплаты", "Абоненты", "Аб", "AversAbon",
            "Поиск квартиры, лицевой счёт, ввод платежей", "Ввод платежей, карточка квартиры", AppModule.Abon,
            [AppModule.Subscriber, AppModule.Print]),
        new(AppModule.Disp, "Заявки АДС", "Заявки", "Зв", "AversDisp",
            "Приём и журнал заявок от населения", "Приём заявок от населения", AppModule.Disp,
            [AppModule.Ticket]),
        new(AppModule.Anten, "Дома и антенны", "Дома", "Дм", "AversAnten",
            "Паспорт дома, схема СКПТ, сведения антенной службы", "Паспорт дома, усилители, ключи", AppModule.Anten,
            [AppModule.Scheme, AppModule.AntenInfo]),
        new(AppModule.Contract, "Договоры", "Договоры", "Дг", "aversContract",
            "Договоры по дому: с кем заключён, когда, статус", "Реестр договоров на обслуживание", AppModule.Abon, []),
        new(AppModule.PrDubl, "Единые квитанции", "Квитанции", "Кв", "PrDublPay",
            "Платежи по единым квитанциям ПЖРЭУ", "Дубли оплат, реестры", AppModule.Abon, []),
        new(AppModule.Domofo, "Домофоны", "Домофоны", "Дф", "domofo",
            "Панели подъездов, ключи, заявки по домофону", "Абоненты домофонов", AppModule.Anten, []),
        new(AppModule.Legal, "Должники", "Должники", "Юр", "Дело-Аверс",
            "Должники, досудебные уведомления, суд", "Претензии, Дело-Аверс", AppModule.Legal,
            [AppModule.Claim]),
        new(AppModule.Ppr, "Графики ППР", "ППР", "ПР", "AversAnten · Сервис",
            "Планово-предупредительные работы по домам", "План и выполнение по домам", AppModule.Anten, []),
        new(AppModule.Reports, "Отчёты", "Отчёты", "От", "Отчёты",
            "Отчёты в Excel", "Итоги по заявкам, должники", AppModule.Legal, []),
        new(AppModule.Phonebook, "Словарь", "Словарь", "Сл", "phonebook",
            "Улицы, дома, материалы, радиомеханики", "Телефоны и справочники", AppModule.Phonebook, []),
        new(AppModule.Reader, "Просмотр дома", "Просмотр", "Чт", "Читатель",
            "Сведения по дому без правки — для радиомехаников", "Только чтение", AppModule.Reader, []),
        new(AppModule.Access, "Права доступа", "Права", "Пр", "Admin",
            "Роли и права пользователей", "Пользователи и роли", AppModule.Access, []),
        new(AppModule.ChangeLog, "Журнал изменений", "Журнал", "Жр", "Журнал",
            "Кто, когда и что поменял: оплаты, договоры, паспорта домов, сотрудники", "Кто и что поменял", AppModule.Access, []),
    ];

    /// <summary>Видит ли роль модуль. Администратор видит всё.</summary>
    public static bool IsAvailable(UserRole role, NavEntry entry) =>
        role == UserRole.Administrator || RoleCatalog.Can(role, entry.AccessModule, out _);

    /// <summary>Кнопка какого модуля подсвечена, пока открыто окно <paramref name="module"/>.</summary>
    public static NavEntry? Owner(AppModule module) =>
        All.FirstOrDefault(e => e.Module == module || e.Children.Contains(module));
}

public sealed partial class NavItem(NavEntry entry, ICommand go) : ObservableObject
{
    public NavEntry Entry { get; } = entry;
    public AppModule Module => Entry.Module;
    public string Title => Entry.Title;
    public string ShortTitle => Entry.ShortTitle;
    public string Glyph => Entry.Glyph;
    public string LegacyName => Entry.LegacyName;
    public string Hint => Entry.Hint;
    public string Summary => Entry.Summary;
    public ICommand Go { get; } = go;

    [ObservableProperty] private bool _isCurrent;
}
