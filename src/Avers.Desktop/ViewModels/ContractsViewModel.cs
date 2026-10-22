using System.Collections.ObjectModel;
using System.Globalization;
using Avers.Desktop.Controls;
using Avers.Desktop.Data;
using Avers.Desktop.Reports;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Экран «Договоры»: выбор дома, поиск договора по номеру или фамилии во всей базе, новый договор,
/// расторжение, новая квартира и реестр дома в Excel. Договор хранится в квартире: новый договор заменяет прежний,
/// прежние данные остаются в журнале изменений.
/// </summary>
public sealed partial class ContractsViewModel(MainViewModel shell) : ObservableObject
{
    public MainViewModel Shell => shell;

    public ObservableCollection<ContractRow> ContractRows { get; } = [];

    [ObservableProperty] private ContractRow? _selectedContractRow;
    [ObservableProperty] private bool _onlyActiveContracts = true;
    [ObservableProperty] private string _contractQuery = "";

    partial void OnSelectedContractRowChanged(ContractRow? value)
    {
        if (value is not null && shell.SelectedSubscriber?.Id != value.Subscriber.Id)
            shell.SelectedSubscriber = value.Subscriber;
    }

    partial void OnOnlyActiveContractsChanged(bool value) => RebuildRows();

    /// <summary>Реестр договоров выбранного дома; выбранная квартира подсвечивается.</summary>
    internal void RebuildRows()
    {
        ContractRows.Clear();
        var list = shell.HouseSubscribers.AsEnumerable();
        if (shell.SelectedHouse is { } house) list = list.Where(s => s.HouseId == house.Id);
        if (OnlyActiveContracts) list = list.Where(s => s.ContractTerminated is null);
        foreach (var s in list.OrderBy(s => s.Section).ThenBy(s => s.Apartment, NaturalOrder.Instance))
            ContractRows.Add(new ContractRow(s, shell.Today, shell.PaymentWarningMonths));
        SelectedContractRow = ContractRows.FirstOrDefault(r => r.Subscriber.Id == shell.SelectedSubscriber?.Id);
    }

    [RelayCommand]
    private void OpenCard(ContractRow? row)
    {
        if (row is not null) shell.SelectedSubscriber = row.Subscriber;
        if (shell.SelectedSubscriber is null) return;
        shell.GoCommand.Execute(AppModule.Subscriber);
    }


    /// <summary>Есть цифры — ищем по номеру договора, нет — по фамилии.</summary>
    [RelayCommand]
    private async Task FindContract()
    {
        var query = ContractQuery.Trim();
        var mode = query.Any(char.IsDigit) ? SearchMode.Contract : SearchMode.Surname;
        try
        {
            var hit = await shell.Store.FindSubscriberAsync(mode, query, "", "", "");
            if (hit is null)
            {
                shell.Status = mode == SearchMode.Contract ? $"Договор «{query}» не найден." : $"Абонент «{query}» не найден.";
                return;
            }
            await shell.ShowSubscriberAsync(hit.Value.Id, hit.Value.HouseId);
        }
        catch (StoreException e)
        {
            shell.Status = e.Message;
            return;
        }
        if (shell.SelectedSubscriber is not { } s) return;
        if (s.ContractTerminated is not null && OnlyActiveContracts)
            OnlyActiveContracts = false;
        shell.Status = $"Найден: {s.FullName}, {s.House.AddressLine}, кв. {s.Apartment}.";
    }

    [ObservableProperty] private bool _isNewApartmentOpen;
    [ObservableProperty] private string _newSection = "";
    [ObservableProperty] private string _newApartment = "";
    [ObservableProperty] private string _newAccount = "";
    [ObservableProperty] private string _newLastName = "";
    [ObservableProperty] private string _newFirstName = "";
    [ObservableProperty] private string _newPatronymic = "";
    [ObservableProperty] private string _newPhone = "";
    [ObservableProperty] private string _newContractNumber = "";
    [ObservableProperty] private string _newContractDate = "";
    [ObservableProperty] private string _newTariff = "";

    [RelayCommand]
    private void StartNewApartment()
    {
        if (shell.SelectedHouse is null || !shell.CanEditCard) return;
        NewSection = NewApartment = NewAccount = NewLastName = NewFirstName = NewPatronymic = NewPhone = NewContractNumber = NewTariff = "";
        NewContractDate = shell.Today.ToString("dd.MM.yyyy");
        IsNewApartmentOpen = true;
    }

    [RelayCommand]
    private void CancelNewApartment() => IsNewApartmentOpen = false;

    [RelayCommand]
    private async Task AddApartment()
    {
        if (shell.SelectedHouse is not { } house || !shell.CanEditCard) return;
        if (!MainViewModel.TryParseDate(NewContractDate, out var signed))
        {
            shell.Status = "Дата договора — в виде дд.мм.гггг.";
            return;
        }
        if (!decimal.TryParse(NewTariff.Trim(), NumberStyles.Number, MainViewModel.Ru, out var tariff))
        {
            shell.Status = "Тариф — сумма в рублях за месяц, например 95 или 95,50.";
            return;
        }
        var account = NewAccount.Trim();
        await shell.Save(async () =>
        {
            await shell.Store.AddApartmentAsync(new Subscriber
            {
                HouseId = house.Id,
                Section = NewSection,
                Apartment = NewApartment,
                Account = account,
                LastName = NewLastName,
                FirstName = NewFirstName,
                Patronymic = NewPatronymic,
                Phone = NewPhone,
                ContractNumber = NewContractNumber,
                ContractDate = signed
            }, tariff);
            IsNewApartmentOpen = false;
            await shell.ReloadAsync();
            if (await shell.Store.FindSubscriberAsync(SearchMode.Account, account, "", "", "") is { } hit)
                await shell.ShowSubscriberAsync(hit.Id, hit.HouseId);
            shell.Status = $"Квартира {NewApartment.Trim()} добавлена, лицевой счёт {account}.";
        });
    }

    [RelayCommand]
    private void ExportContracts()
    {
        if (shell.SelectedHouse is not { } house) return;
        shell.Export("договоры", $"реестр договоров {house.AddressLine}", path => XlsxTable.Write(path,
            $"Реестр договоров · {house.AddressLine} · на {shell.Today:dd.MM.yyyy}",
            ["Секц", "Кв", "Лиц. счёт", "Фамилия И.О.", "№ договора", "С кем заключён", "Заключён", "Расторгнут", "Посл. оплата", "Примечания"],
            ContractRows.Select(r => new object?[]
                { r.Section, r.Apartment, r.Account, r.FullName, r.Number, r.Party, r.Signed, r.Terminated, r.LastPay, r.Notes })));
    }
}

/// <summary>Строка реестра договоров по дому: жёлтая — давно не было оплаты, серая — договор расторгнут.</summary>
public sealed class ContractRow(Subscriber s, DateOnly today, int warningMonths) : IRowTone
{
    public Subscriber Subscriber { get; } = s;
    public string Section => Subscriber.Section;
    public string Apartment => Subscriber.Apartment;
    public string Account => Subscriber.Account;
    public string FullName => Subscriber.FullName;
    public string Number => Subscriber.ContractNumber;
    public string Party => Subscriber.ContractParty;
    public string Signed => Subscriber.ContractDate?.ToString("dd.MM.yyyy") ?? "";
    public string Terminated => Subscriber.ContractTerminated?.ToString("dd.MM.yyyy") ?? "";
    public string Notes => Subscriber.Notes;
    public string LastPay => Subscriber.LastPaymentDate?.ToString("dd.MM.yyyy") ?? "нет";
    public string MonthsWithoutPay => PaymentAge.MonthsSince(Subscriber.LastPaymentDate, today)?.ToString(CultureInfo.InvariantCulture) ?? "—";
    public bool IsLate { get; } = PaymentAge.IsLate(s, today, warningMonths);
    public RowTone Tone => Subscriber.ContractTerminated is not null ? RowTone.Off : IsLate ? RowTone.Warn : RowTone.None;
}
