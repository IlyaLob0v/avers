using System.Collections.ObjectModel;
using Avers.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avers.Desktop.ViewModels;

/// <summary>Журнал изменений и журнал просмотров персональных данных: только просмотр, только администратор.</summary>
public sealed partial class ChangeLogViewModel(MainViewModel shell) : ObservableObject
{
    public ObservableCollection<ChangeLogRow> ChangeLogRows { get; } = [];

    /// <summary>false — кто что изменил, true — кто чьи персональные данные открыл (152-ФЗ).</summary>
    [ObservableProperty] private bool _showViews;

    // Номер последнего запроса: ответ более раннего (например, до переключения журнала) не затирает свежий.
    private int _loadVersion;

    public string ChangeLogHint => ShowViews
        ? $"Кто открывал данные абонентов: поиск, абоненты дома, карточка, должники, сканы. Последние {ChangeLog.Shown} записей, новые сверху."
        : $"Последние {ChangeLog.Shown} записей, новые сверху. Записи журнала нельзя изменить или удалить.";

    partial void OnShowViewsChanged(bool value)
    {
        OnPropertyChanged(nameof(ChangeLogHint));
        _ = LoadAsync();
    }

    /// <summary>Журнал перечитываем при каждом входе в модуль.</summary>
    public Task LoadAsync() =>
        shell.User?.Role == UserRole.Administrator
            ? shell.Save(async () =>
            {
                var version = ++_loadVersion;
                var rows = ShowViews
                    ? (await shell.Store.LoadAccessLogAsync()).Select(e => new ChangeLogRow(e))
                    : (await shell.Store.LoadChangeLogAsync()).Select(e => new ChangeLogRow(e));
                if (version != _loadVersion)
                    return;
                ChangeLogRows.Clear();
                foreach (var row in rows.ToList())
                    ChangeLogRows.Add(row);
            })
            : Task.CompletedTask;
}

public sealed class ChangeLogRow
{
    public ChangeLogRow(ChangeLogEntry entry) : this(entry.At, entry.UserName, entry.Action, entry.Target) => Changes = entry.Changes;

    public ChangeLogRow(AccessLogEntry entry) : this(entry.At, entry.UserName, entry.Action, entry.Target) { }

    private ChangeLogRow(DateTimeOffset at, string userName, string action, string target)
    {
        At = at.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        UserName = userName;
        Action = action;
        Target = target;
    }

    public string At { get; }
    public string UserName { get; }
    public string Action { get; }
    public string Target { get; }
    public string Changes { get; } = "";
}
