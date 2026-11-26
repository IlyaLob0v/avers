using Avers.Contracts;
using Avers.Desktop.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty] private IReadOnlyList<MobileTicketDto> _readerTickets = [];
    [ObservableProperty] private string _readerTicketsStatus = "Выберите дом.";
    [ObservableProperty] private bool _isLoadingReaderTickets;
    private int _readerTicketsVersion;
    private int? _readerTicketsHouse;

    private void ClearReaderTickets()
    {
        ++_readerTicketsVersion;
        _readerTicketsHouse = null;
        ReaderTickets = [];
        IsLoadingReaderTickets = false;
        ReaderTicketsStatus = "Выберите дом.";
    }

    [RelayCommand]
    private Task RefreshReaderTickets() => LoadReaderTicketsAsync(force: true);

    private async Task LoadReaderTicketsAsync(bool force = false)
    {
        var user = User;
        if (Module != AppModule.Reader || user is null || SelectedHouse is not { } house)
        {
            ClearReaderTickets();
            return;
        }
        if (!force && _readerTicketsHouse == house.Id) return;
        var version = ++_readerTicketsVersion;
        _readerTicketsHouse = house.Id;
        ReaderTickets = [];
        IsLoadingReaderTickets = true;
        ReaderTicketsStatus = "Загрузка заявок…";
        bool IsCurrent() => version == _readerTicketsVersion && User == user
            && SelectedHouse?.Id == house.Id && Module == AppModule.Reader;
        try
        {
            var result = await _store.LoadMobileTicketsAsync(house.Id);
            if (!IsCurrent()) return;
            ReaderTickets = result.Items;
            ReaderTicketsStatus = result.Total == 0 ? "Открытых заявок нет."
                : result.Total > result.Items.Count ? $"Показаны последние {result.Items.Count} из {result.Total} открытых заявок."
                : $"Открытых заявок: {result.Total}.";
        }
        catch (StoreException error)
        {
            if (IsCurrent())
            {
                _readerTicketsHouse = null;
                ReaderTicketsStatus = error.Message;
            }
        }
        finally
        {
            if (IsCurrent()) IsLoadingReaderTickets = false;
        }
    }
}
