using Avers.Desktop.Data;
using Avers.Desktop.Reports;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Экран «Договоры»: выбор дома, поиск договора по номеру или фамилии во всей базе, новый договор,
/// расторжение и реестр дома в Excel. Договор хранится в квартире: новый договор заменяет прежний,
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
