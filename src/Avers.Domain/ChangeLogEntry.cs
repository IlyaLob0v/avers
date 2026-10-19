namespace Avers.Domain;

/// <summary>
/// Запись журнала изменений: кто, когда и что поменял. Записи только добавляются,
/// в программе их нельзя ни изменить, ни удалить.
/// </summary>
public sealed class ChangeLogEntry
{
    public int Id { get; set; }
    public DateTimeOffset At { get; set; }

    /// <summary>Сотрудник: «Орлов С.А. (orlov)».</summary>
    public string UserName { get; set; } = "";

    /// <summary>Что сделано: «Оплата за 08.2026», «Паспорт дома».</summary>
    public string Action { get; set; } = "";

    /// <summary>С чем: лицевой счёт, дом или сотрудник.</summary>
    public string Target { get; set; } = "";

    /// <summary>По строке на поле: «Поле: было → стало».</summary>
    public string Changes { get; set; } = "";
}
