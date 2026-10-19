using System.Collections.ObjectModel;
using Avers.Data;
using Avers.Domain;

namespace Avers.Desktop.ViewModels;

/// <summary>Журнал изменений: только просмотр, только администратор.</summary>
public sealed partial class MainViewModel
{
    public ObservableCollection<ChangeLogRow> ChangeLogRows { get; } = [];

    public string ChangeLogHint =>
        $"Последние {ChangeLog.Shown} записей, новые сверху. Записи журнала нельзя изменить или удалить.";

    /// <summary>Журнал перечитываем при каждом входе в модуль.</summary>
    private Task LoadChangeLogAsync() =>
        User?.Role == UserRole.Administrator
            ? Save(async () =>
            {
                var entries = await _store.LoadChangeLogAsync();
                ChangeLogRows.Clear();
                foreach (var e in entries)
                    ChangeLogRows.Add(new ChangeLogRow(e));
            })
            : Task.CompletedTask;
}

public sealed class ChangeLogRow(ChangeLogEntry entry)
{
    public string At { get; } = entry.At.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public string UserName => entry.UserName;
    public string Action => entry.Action;
    public string Target => entry.Target;
    public string Changes => entry.Changes;
}
