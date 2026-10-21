using System.Globalization;
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
public sealed partial class MainViewModel
{
    [ObservableProperty] private string _contractQuery = "";

    /// <summary>Есть цифры — ищем по номеру договора, нет — по фамилии.</summary>
    [RelayCommand]
    private async Task FindContract()
    {
        var query = ContractQuery.Trim();
        var mode = query.Any(char.IsDigit) ? SearchMode.Contract : SearchMode.Surname;
        try
        {
            var hit = await _store.FindSubscriberAsync(mode, query, "", "", "");
            if (hit is null)
            {
                Status = mode == SearchMode.Contract ? $"Договор «{query}» не найден." : $"Абонент «{query}» не найден.";
                return;
            }
            await ShowSubscriberAsync(hit.Value.Id, hit.Value.HouseId);
        }
        catch (StoreException e)
        {
            Status = e.Message;
            return;
        }
        if (SelectedSubscriber is not { } s) return;
        if (s.ContractTerminated is not null && OnlyActiveContracts)
            OnlyActiveContracts = false;
        Status = $"Найден: {s.FullName}, {s.House.AddressLine}, кв. {s.Apartment}.";
    }

    [RelayCommand]
    private void NewContract()
    {
        if (SelectedSubscriber is not { } s) return;
        EditContractNumber = "";
        EditContractDate = _today.ToString("dd.MM.yyyy");
        EditContractTerminated = "";
        EditContractHolder = "";
        Status = $"Новый договор для кв. {s.Apartment}: впишите номер и нажмите «Сохранить». Прежний договор останется в журнале изменений.";
    }

    [RelayCommand]
    private async Task TerminateContract()
    {
        if (SelectedSubscriber is null || !CanEditCard) return;
        EditContractTerminated = _today.ToString("dd.MM.yyyy");
        await SaveCard();
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
        if (SelectedHouse is null || !CanEditCard) return;
        NewSection = NewApartment = NewAccount = NewLastName = NewFirstName = NewPatronymic = NewPhone = NewContractNumber = NewTariff = "";
        NewContractDate = _today.ToString("dd.MM.yyyy");
        IsNewApartmentOpen = true;
    }

    [RelayCommand]
    private void CancelNewApartment() => IsNewApartmentOpen = false;

    [RelayCommand]
    private async Task AddApartment()
    {
        if (SelectedHouse is not { } house || !CanEditCard) return;
        if (!TryParseDate(NewContractDate, out var signed))
        {
            Status = "Дата договора — в виде дд.мм.гггг.";
            return;
        }
        if (!decimal.TryParse(NewTariff.Trim(), NumberStyles.Number, Ru, out var tariff))
        {
            Status = "Тариф — сумма в рублях за месяц, например 95 или 95,50.";
            return;
        }
        var account = NewAccount.Trim();
        await Save(async () =>
        {
            await _store.AddApartmentAsync(new Subscriber
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
            await ReloadAsync();
            if (await _store.FindSubscriberAsync(SearchMode.Account, account, "", "", "") is { } hit)
                await ShowSubscriberAsync(hit.Id, hit.HouseId);
            Status = $"Квартира {NewApartment.Trim()} добавлена, лицевой счёт {account}.";
        });
    }

    [RelayCommand]
    private void ExportContracts()
    {
        if (SelectedHouse is not { } house) return;
        Export("договоры", $"реестр договоров {house.AddressLine}", path => XlsxTable.Write(path,
            $"Реестр договоров · {house.AddressLine} · на {_today:dd.MM.yyyy}",
            ["Секц", "Кв", "Лиц. счёт", "Фамилия И.О.", "№ договора", "С кем заключён", "Заключён", "Расторгнут", "Посл. оплата", "Примечания"],
            ContractRows.Select(r => new object?[]
                { r.Section, r.Apartment, r.Account, r.FullName, r.Number, r.Party, r.Signed, r.Terminated, r.LastPay, r.Notes })));
    }
}
