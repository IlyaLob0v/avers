using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.Reports;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Единые квитанции ПЖРЭУ: поиск квартиры по любому лицевому счёту во всей базе и отбор строк по месяцу.
/// Квитанции делает ОЭК (мы в них услуга с кодом 38): её файл начислений загружается здесь же.
/// </summary>
public sealed partial class MainViewModel
{
    private int? _receiptsBoundId;

    public ObservableCollection<ReceiptPeriod> ReceiptPeriods { get; } = [];

    [ObservableProperty] private ReceiptPeriod? _selectedReceiptPeriod;
    [ObservableProperty] private string _receiptAccount = "";

    /// <summary>Месяц файла ОЭК, мм.гггг; по умолчанию прошлый: файл за месяц приходит в следующем.</summary>
    [ObservableProperty] private string _oekMonth = "";

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

    [RelayCommand]
    private async Task ImportOek()
    {
        if (await PickOekFileAsync() is { } path)
            await ImportOekFileAsync(path);
    }

    /// <summary>Загружает файл ОЭК за <see cref="OekMonth"/>; расхождения открываются в Excel.</summary>
    public async Task ImportOekFileAsync(string path)
    {
        if (!CanEditSettings)
        {
            Status = "Файл ОЭК загружают администратор и начальник АДС.";
            return;
        }
        if (!DateOnly.TryParseExact("01." + OekMonth.Trim(), ["dd.MM.yyyy", "dd.M.yyyy"], Ru, DateTimeStyles.None, out var month))
        {
            Status = "Месяц файла ОЭК — в виде мм.гггг.";
            return;
        }
        List<OekRow> rows;
        try
        {
            rows = OekFile.Read(path);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Status = $"Не удалось прочитать файл ОЭК: {e.Message}";
            return;
        }
        await Save(async () =>
        {
            var r = await _store.ImportOekAsync(new OekImportRequest(month.Year, month.Month, rows));
            await ReloadAsync();
            if (r.Mismatches.Count > 0)
                Export("сверка-оэк", "сверка с ОЭК", file => XlsxTable.Write(file,
                    $"Сверка с ОЭК за {month:MM.yyyy}: строки, которые надо разобрать",
                    ["Л/с ОЭК", "Л/с Аверса", "Адрес", "Кв.", "Начислено ОЭК", "Тариф Аверса", "Оплачено ОЭК", "Что не так"],
                    r.Mismatches.Select(m => new object?[] { m.OekAccount, m.Account, m.Address, m.Apartment, m.OekAccrued, m.AversTariff, m.OekPaid, m.Problem })));
            Status = $"Файл ОЭК за {month:MM.yyyy}: строк {r.Rows}, найдено квартир {r.Matched}, загружено оплат {r.Payments} на {Money(r.PaidTotal)}, " +
                     $"уже были загружены {r.AlreadyLoaded}, расхождений {r.Mismatches.Count}.";
        });
    }

    private static async Task<string?> PickOekFileAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return null;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Файл начислений ОЭК",
            FileTypeFilter = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}

/// <summary>Месяц отбора единых квитанций; Month = null — все месяцы.</summary>
public sealed record ReceiptPeriod(DateOnly? Month, string Title)
{
    public static ReceiptPeriod All { get; } = new(null, "Все периоды");
}
