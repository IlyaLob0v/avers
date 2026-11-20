using System.Collections.ObjectModel;
using Avers.Data;
using Avers.Desktop.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

public sealed partial class SubscriberArchiveViewModel(MainViewModel shell) : ObservableObject
{
    private int? _subscriberId;
    private int _requestVersion;
    private int _offset;
    private int _total;
    private int? _loadedYear;
    public ObservableCollection<JackPayment> Rows { get; } = [];
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _yearText = "";
    [ObservableProperty] private string _status = "Откройте архив для просмотра исходных операций.";

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) _ = LoadAsync();
    }

    partial void OnIsLoadingChanged(bool value) => NotifyPaging();
    private bool CanPrevious => !IsLoading && _offset > 0;
    private bool CanNext => !IsLoading && _offset + Rows.Count < _total;

    private void NotifyPaging()
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    internal void SelectSubscriber(int? id)
    {
        if (_subscriberId == id) return;
        ++_requestVersion;
        _subscriberId = id;
        _offset = _total = 0;
        _loadedYear = null;
        Rows.Clear();
        IsExpanded = false;
        IsLoading = false;
        YearText = "";
        Status = "Откройте архив для просмотра исходных операций.";
        NotifyPaging();
    }

    [RelayCommand]
    private Task Reload() => LoadAsync();
    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private Task Previous() => LoadAsync(_offset - SubscriberArchive.PageSize);
    [RelayCommand(CanExecute = nameof(CanNext))]
    private Task Next() => LoadAsync(_offset + SubscriberArchive.PageSize);

    private async Task LoadAsync(int offset = 0)
    {
        if (_subscriberId is not { } id) return;
        int? year = null;
        if (!string.IsNullOrWhiteSpace(YearText))
        {
            if (!int.TryParse(YearText.Trim(), out var y) || y is < 1 or > 9998)
            {
                Status = "Введите расчётный год либо оставьте поле пустым.";
                return;
            }
            year = y;
        }
        var version = ++_requestVersion;
        if (year != _loadedYear) offset = 0;
        IsLoading = true;
        Status = "Загрузка архива…";
        try
        {
            var page = await shell.Store.LoadArchiveAsync(id, year, offset);
            if (version != _requestVersion) return;
            _offset = page.Offset;
            _loadedYear = year;
            _total = page.TotalCount;
            Rows.Clear();
            foreach (var row in page.Rows) Rows.Add(row);
            Status = _total == 0 ? "Исходных операций JACK по выбранным условиям нет." : $"Операции {_offset + 1}–{_offset + Rows.Count} из {_total}";
        }
        catch (StoreException e)
        {
            if (version == _requestVersion) Status = e.Message;
        }
        finally
        {
            if (version == _requestVersion)
            {
                IsLoading = false;
                NotifyPaging();
            }
        }
    }
}
