using System.Windows.Input;
using Avalonia.Media;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avers.Desktop.ViewModels;

/// <summary>Модуль в панели навигации, меню «Модули» и на рабочем столе.</summary>
public sealed record NavEntry(
    AppModule Module,
    string Title,
    string ShortTitle,
    Geometry Icon,
    string Hint,
    string Summary,
    AppModule AccessModule,
    AppModule[] Children);

/// <summary>Доступные сотруднику разделы одной группы верхнего меню.</summary>
public sealed record NavGroup(string Title, IReadOnlyList<NavItem> Items);

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
        new(AppModule.Abon, "Абоненты и оплаты", "Абоненты", Geometry.Parse("M8,10 A4,4 0 1 0 8,2 A4,4 0 1 0 8,10 M1,21 V17 Q1,13 8,13 Q15,13 15,17 V21 M17,3 Q23,5 18,10 M17,13 Q23,14 23,18 V21"),
            "Поиск квартиры, лицевой счёт, ввод платежей", "Ввод платежей, карточка квартиры", AppModule.Abon,
            [AppModule.Subscriber, AppModule.Print]),
        new(AppModule.Disp, "Заявки АДС", "Заявки", Geometry.Parse("M3,13 V11 A9,9 0 0 1 21,11 V17 Q21,22 12,22 M3,12 H7 V18 H3 Z M17,12 H21 V18 H17 Z"),
            "Приём и журнал заявок от населения", "Приём заявок от населения", AppModule.Disp,
            [AppModule.Ticket]),
        new(AppModule.Anten, "Дома и антенны", "Дома", Geometry.Parse("M2,21 H22 M4,21 V5 H13 V21 M13,10 H20 V21 M7,9 H10 M7,13 H10 M7,17 H10 M16,13 H18 M16,17 H18"),
            "Паспорт дома, схема СКПТ, сведения антенной службы", "Паспорт дома, усилители, ключи", AppModule.Anten,
            [AppModule.Scheme, AppModule.AntenInfo]),
        new(AppModule.Contract, "Договоры", "Договоры", Geometry.Parse("M5,2 H14 L19,7 V22 H5 Z M14,2 V7 H19 M8,11 H16 M8,15 H16 M8,19 L10,17 L12,19 H16"),
            "Договоры по дому: с кем заключён, когда, статус", "Реестр договоров на обслуживание", AppModule.Abon, []),
        new(AppModule.PrDubl, "Платежи ОЭК", "Платежи ОЭК", Geometry.Parse("M5,2 L8,4 L12,2 L16,4 L19,2 V22 L16,20 L12,22 L8,20 L5,22 Z M8,8 H16 M8,12 H16 M8,16 H13"),
            "Платежи по единым квитанциям ОЭК", "Поиск по счёту, сверка и загрузка файла", AppModule.Abon, []),
        new(AppModule.Domofo, "Домофоны", "Домофоны", Geometry.Parse("M5,2 H19 V22 H5 Z M8,6 H16 V9 H8 Z M9,13 H10 M14,13 H15 M9,17 H10 M14,17 H15"),
            "Панели подъездов, ключи, заявки по домофону", "Абоненты домофонов", AppModule.Anten, []),
        new(AppModule.Legal, "Должники", "Должники", Geometry.Parse("M12,2 V21 M7,21 H17 M3,6 H21 M5,6 L2,14 H8 Z M19,6 L16,14 H22 Z"),
            "Должники, досудебные уведомления, суд", "Претензии, судебные дела", AppModule.Legal,
            [AppModule.Claim]),
        new(AppModule.Ppr, "Графики ППР", "ППР", Geometry.Parse("M3,5 H21 V21 H3 Z M7,2 V7 M17,2 V7 M3,10 H21 M8,15 L11,18 L16,13"),
            "Планово-предупредительные работы по домам", "План и выполнение по домам", AppModule.Anten, []),
        new(AppModule.Reports, "Отчёты", "Отчёты", Geometry.Parse("M3,3 V21 H22 M7,17 V12 H10 V17 Z M12,17 V8 H15 V17 Z M17,17 V4 H20 V17 Z"),
            "Отчёты в Excel", "Итоги по заявкам, должники", AppModule.Legal, []),
        new(AppModule.Phonebook, "Справочники", "Справочники", Geometry.Parse("M12,6 Q7,2 2,4 V19 Q7,17 12,21 Q17,17 22,19 V4 Q17,2 12,6 V21 M5,8 L9,9 M15,9 L19,8"),
            "Улицы, дома, материалы, радиомеханики", "Телефоны и справочники", AppModule.Phonebook, []),
        new(AppModule.Reader, "Просмотр дома", "Просмотр дома", Geometry.Parse("M2,12 Q12,-1 22,12 Q12,25 2,12 Z M12,15 A3,3 0 1 0 12,9 A3,3 0 1 0 12,15"),
            "Сведения по дому без правки — для радиомехаников", "Только чтение", AppModule.Reader, []),
        new(AppModule.Access, "Права доступа", "Права", Geometry.Parse("M12,2 L21,6 V11 Q21,17 12,22 Q3,17 3,11 V6 Z M8,12 L11,15 L16,9"),
            "Роли и права пользователей", "Пользователи и роли", AppModule.Access, []),
        new(AppModule.ChangeLog, "Журнал изменений", "Журнал", Geometry.Parse("M3,5 H13 M3,10 H10 M3,15 H8 M3,20 H8 M16,22 A6,6 0 1 0 16,10 A6,6 0 1 0 16,22 M16,12 V16 L19,18"),
            "Кто, когда и что поменял: оплаты, договоры, дома, заявки, ППР, сотрудники", "Кто и что поменял", AppModule.Access, []),
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
    public Geometry Icon => Entry.Icon;
    public string Hint => Entry.Hint;
    public string Summary => Entry.Summary;
    public ICommand Go { get; } = go;

    [ObservableProperty] private bool _isCurrent;
}
