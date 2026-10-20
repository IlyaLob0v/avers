using System.Collections.ObjectModel;
using Avers.Desktop.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Единые квитанции ПЖРЭУ: поиск квартиры по любому лицевому счёту во всей базе и отбор строк по месяцу.
/// Загрузки файлов ПЖРЭУ пока нет: ждём образец файла.
/// </summary>
public sealed partial class MainViewModel
{
    private int? _receiptsBoundId;

    public ObservableCollection<ReceiptPeriod> ReceiptPeriods { get; } = [];

    [ObservableProperty] private ReceiptPeriod? _selectedReceiptPeriod;
    [ObservableProperty] private string _receiptAccount = "";

    public string ReceiptOwner => SelectedSubscriber is { } s ? $"{s.FullName} · {s.House.AddressLine}, кв. {s.Apartment}" : "";

    private IEnumerable<UnifiedReceipt> SubscriberReceipts => SelectedSubscriber?.Receipts ?? [];

    partial void OnSelectedReceiptPeriodChanged(ReceiptPeriod? value) => RebuildReceipts();

    /// <summary>Месяцы, за которые у квартиры есть строки, новые сверху; выбор месяца сохраняется, пока квартира та же.</summary>
    private void RebuildReceiptPeriods()
    {
        var keep = _receiptsBoundId == SelectedSubscriber?.Id ? SelectedReceiptPeriod : null;
        if (_receiptsBoundId != SelectedSubscriber?.Id)
            ReceiptAccount = SelectedSubscriber?.Account ?? "";
        _receiptsBoundId = SelectedSubscriber?.Id;

        ReceiptPeriods.Clear();
        ReceiptPeriods.Add(ReceiptPeriod.All);
        foreach (var m in SubscriberReceipts.Select(r => new DateOnly(r.Moment.Year, r.Moment.Month, 1)).Distinct().OrderDescending())
            ReceiptPeriods.Add(new ReceiptPeriod(m, $"За {Ru.DateTimeFormat.MonthNames[m.Month - 1].ToLowerInvariant()} {m.Year}"));
        SelectedReceiptPeriod = ReceiptPeriods.FirstOrDefault(p => p == keep) ?? ReceiptPeriod.All;
        RebuildReceipts();
        OnPropertyChanged(nameof(ReceiptOwner));
    }

    private void RebuildReceipts()
    {
        Receipts.Clear();
        var month = SelectedReceiptPeriod?.Month;
        foreach (var r in SubscriberReceipts
                     .Where(r => month is null || (r.Moment.Year == month.Value.Year && r.Moment.Month == month.Value.Month))
                     .OrderByDescending(r => r.Moment))
            Receipts.Add(r);
    }

    [RelayCommand]
    private async Task FindReceipts()
    {
        var account = ReceiptAccount.Trim();
        try
        {
            var hit = await _store.FindSubscriberAsync(SearchMode.Account, account, "", "", "");
            if (hit is null)
            {
                Status = $"Лицевой счёт {account} не найден.";
                return;
            }
            await ShowSubscriberAsync(hit.Value.Id, hit.Value.HouseId);
        }
        catch (StoreException e)
        {
            Status = e.Message;
            return;
        }
        Status = Receipts.Count == 0
            ? $"По лицевому счёту {SelectedSubscriber?.Account} единых квитанций нет."
            : $"Лицевой счёт {SelectedSubscriber?.Account}: строк — {Receipts.Count}.";
    }
}

/// <summary>Месяц отбора единых квитанций; Month = null — все месяцы.</summary>
public sealed record ReceiptPeriod(DateOnly? Month, string Title)
{
    public static ReceiptPeriod All { get; } = new(null, "Все периоды");
}
