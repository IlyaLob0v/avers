namespace Avers.Domain;

/// <summary>
/// Запись журнала просмотров персональных данных (152-ФЗ): кто, когда и чьи данные открыл.
/// Пишет сервер; записи только добавляются.
/// </summary>
public sealed class AccessLogEntry
{
    public int Id { get; set; }
    public DateTimeOffset At { get; set; }

    /// <summary>Сотрудник: «Орлов С.А. (orlov)».</summary>
    public string UserName { get; set; } = "";

    /// <summary>Что открыто: «Карточка абонента», «Абоненты дома».</summary>
    public string Action { get; set; } = "";

    /// <summary>Чьи данные: лицевой счёт, дом или текст поиска.</summary>
    public string Target { get; set; } = "";
}
