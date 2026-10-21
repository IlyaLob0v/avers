using System.Collections.ObjectModel;
using System.Globalization;
using Avers.Contracts;
using Avers.Data;
using Avers.Desktop.Controls;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Карточка квартиры, договор и оплаты: лицевой счёт, фамилия, кем абонент приходится квартире,
/// договор и с кем он заключён, последняя оплата и кто её внёс, сколько месяцев без оплаты (жёлтая подсветка),
/// заявки по квартире.
/// </summary>
public sealed partial class MainViewModel
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private int? _cardBoundId;

    public ObservableCollection<ContractRow> ContractRows { get; } = [];
    public ObservableCollection<CardPaymentRow> CardPayments { get; } = [];
    public ObservableCollection<WorkTicket> ApartmentTicketRows { get; } = [];
    public ObservableCollection<int> CardPaymentYears { get; } = [];
    public ObservableCollection<YearOption> CardTicketYears { get; } = [];

    [ObservableProperty] private ContractRow? _selectedContractRow;
    [ObservableProperty] private int? _cardPaymentYear;
    [ObservableProperty] private YearOption? _cardTicketYear;
    [ObservableProperty] private int _paymentWarningMonths = PaymentAge.DefaultWarningMonths;
    [ObservableProperty] private decimal? _editWarningMonths = PaymentAge.DefaultWarningMonths;
    [ObservableProperty] private string _editContractNumber = "";
    [ObservableProperty] private string _editContractDate = "";
    [ObservableProperty] private string _editContractTerminated = "";
    [ObservableProperty] private string _editContractHolder = "";
    [ObservableProperty] private string _editNotes = "";

    public string CardSurname => SelectedSubscriber?.LastName ?? "";
    public string CardNameRest => SelectedSubscriber is null ? "" : $"{SelectedSubscriber.FirstName} {SelectedSubscriber.Patronymic}".Trim();
    public string CardAccount => SelectedSubscriber?.Account ?? "";
    public string CardAddress => SelectedSubscriber is null ? "" :
        $"{SelectedSubscriber.House.AddressLine}, секц. {SelectedSubscriber.Section}, кв. {SelectedSubscriber.Apartment}";
    /// <summary>Обслуживающая дом организация (в старом Аверсе — «домовладелец»).</summary>
    public string CardOrganization => SelectedSubscriber is null ? "" : ServiceOrganizations.Title(HouseOf(SelectedSubscriber).Organization);
    public string CardOrganizationPhones => SelectedSubscriber is null ? "" : HouseOf(SelectedSubscriber).Organization?.Phones ?? "";
    public string CardContractLine
    {
        get
        {
            if (SelectedSubscriber is not { } s) return "";
            var number = string.IsNullOrWhiteSpace(s.ContractNumber) ? "без номера" : $"№ {s.ContractNumber}";
            var date = s.ContractDate is { } d ? $" от {d:dd.MM.yyyy}" : "";
            var state = s.ContractTerminated is { } t ? $", расторгнут {t:dd.MM.yyyy}" : ", действует";
            return $"Договор {number}{date}{state}. Заключён с: {s.ContractParty}";
        }
    }

    /// <summary>Кнопка «Договор» на экране оплат: номер и дата, по клику — в договор.</summary>
    public string CardContractShort => SelectedSubscriber is not { } s ? "" :
        $"{(string.IsNullOrWhiteSpace(s.ContractNumber) ? "б/н" : s.ContractNumber)}{(s.ContractDate is { } d ? $" от {d:dd.MM.yyyy}" : "")} →";

    public bool IsPaymentLate => SelectedSubscriber is not null && PaymentAge.IsLate(SelectedSubscriber, _today, PaymentWarningMonths);
    public string LastPaymentAgo => SelectedSubscriber is null ? "" : PaymentAge.AgoText(SelectedSubscriber.LastPaymentDate, _today);

    /// <summary>«Последняя оплата 09.07.2026 · 95,00 ₽ за июль 2026 · ОЭК · внёс: … · оплатил: …».</summary>
    public string LastPaymentLine
    {
        get
        {
            if (SelectedSubscriber is not { } s) return "";
            var last = PaymentAge.LastPayment(s.Payments);
            if (last is null || (s.LastPaymentDate is { } d && last.PaidAt < d))
                return s.LastPaymentDate is { } only
                    ? $"Последняя оплата {only:dd.MM.yyyy} (подробностей нет в базе)"
                    : "Оплат не было";
            var parts = new List<string>
            {
                $"Последняя оплата {last.PaidAt:dd.MM.yyyy}",
                $"{last.Amount:N2} ₽ за {MonthName(last.Month)} {last.Year}"
            };
            if (!string.IsNullOrWhiteSpace(last.Source)) parts.Add(last.Source);
            parts.Add($"внёс: {(string.IsNullOrWhiteSpace(last.RecordedBy) ? "не записано" : last.RecordedBy)}");
            if (!string.IsNullOrWhiteSpace(last.PayerName)) parts.Add($"оплатил: {last.PayerName}");
            return string.Join(" · ", parts);
        }
    }

    public string PaymentWarningHint =>
        $"Жёлтым подсвечивается, если без оплаты {PaymentAge.MonthsText(PaymentWarningMonths)} и больше " +
        "(если оплат не было — считается от даты договора).";

    public bool CanEditCard => User is { } u && (u.CanEditAccount || u.Role is UserRole.Administrator or UserRole.AdsHead);
    public bool CanEditSettings => User?.Role is UserRole.Administrator or UserRole.AdsHead;

    partial void OnCardPaymentYearChanged(int? value) => RebuildCardPayments();
    partial void OnCardTicketYearChanged(YearOption? value) => RebuildApartmentTickets();
    partial void OnPaymentWarningMonthsChanged(int value)
    {
        EditWarningMonths = value;
        RefreshCard();
    }

    partial void OnSelectedContractRowChanged(ContractRow? value)
    {
        if (value is not null && SelectedSubscriber?.Id != value.Subscriber.Id)
            SelectedSubscriber = value.Subscriber;
    }

    /// <summary>Вызывается из <c>RefreshDerived</c>: всё, что зависит от выбранной квартиры.</summary>
    private void RefreshCard()
    {
        ContractRows.Clear();
        var list = _subscribers.AsEnumerable();
        if (SelectedHouse is not null) list = list.Where(s => s.HouseId == SelectedHouse.Id);
        if (OnlyActiveContracts) list = list.Where(s => s.ContractTerminated is null);
        foreach (var s in list.OrderBy(s => s.Section).ThenBy(s => s.Apartment, NaturalOrder.Instance))
            ContractRows.Add(new ContractRow(s, _today, PaymentWarningMonths));
        SelectedContractRow = ContractRows.FirstOrDefault(r => r.Subscriber.Id == SelectedSubscriber?.Id);

        if (_cardBoundId != SelectedSubscriber?.Id)
        {
            _cardBoundId = SelectedSubscriber?.Id;
            BindCardEditors();

            CardPaymentYears.Clear();
            foreach (var y in SelectedSubscriber?.Payments.Select(p => p.Year).Distinct().OrderDescending() ?? Enumerable.Empty<int>())
                CardPaymentYears.Add(y);
            CardPaymentYear = CardPaymentYears.Contains(_today.Year) ? _today.Year : CardPaymentYears.FirstOrDefault();

            CardTicketYears.Clear();
            CardTicketYears.Add(new YearOption(null, "Все годы"));
            var ticketYears = SelectedSubscriber is null ? Enumerable.Empty<int>() :
                ApartmentTickets.For(_tickets, SelectedSubscriber).Select(t => t.Date.Year).Distinct().OrderDescending();
            foreach (var y in ticketYears)
                CardTicketYears.Add(new YearOption(y, y.ToString(CultureInfo.InvariantCulture)));
            CardTicketYear = CardTicketYears[0];
        }

        RebuildCardPayments();
        RebuildApartmentTickets();

        OnPropertyChanged(nameof(CardSurname));
        OnPropertyChanged(nameof(CardNameRest));
        OnPropertyChanged(nameof(CardAccount));
        OnPropertyChanged(nameof(CardAddress));
        OnPropertyChanged(nameof(CardOrganization));
        OnPropertyChanged(nameof(CardOrganizationPhones));
        OnPropertyChanged(nameof(CardContractLine));
        OnPropertyChanged(nameof(CardContractShort));
        OnPropertyChanged(nameof(IsPaymentLate));
        OnPropertyChanged(nameof(LastPaymentAgo));
        OnPropertyChanged(nameof(LastPaymentLine));
        OnPropertyChanged(nameof(PaymentWarningHint));
        OnPropertyChanged(nameof(CanEditCard));
        OnPropertyChanged(nameof(CanEditSettings));
    }

    private void BindCardEditors()
    {
        var s = SelectedSubscriber;
        EditContractNumber = s?.ContractNumber ?? "";
        EditContractDate = s?.ContractDate?.ToString("dd.MM.yyyy") ?? "";
        EditContractTerminated = s?.ContractTerminated?.ToString("dd.MM.yyyy") ?? "";
        EditContractHolder = s?.ContractHolder ?? "";
        EditNotes = s?.Notes ?? "";
    }

    private void RebuildCardPayments()
    {
        CardPayments.Clear();
        if (SelectedSubscriber is null || CardPaymentYear is null) return;
        foreach (var p in SelectedSubscriber.Payments.Where(p => p.Year == CardPaymentYear).OrderBy(p => p.Month))
            CardPayments.Add(new CardPaymentRow(p, MonthName(p.Month)));
    }

    private void RebuildApartmentTickets()
    {
        ApartmentTicketRows.Clear();
        if (SelectedSubscriber is null) return;
        foreach (var t in ApartmentTickets.For(_tickets, SelectedSubscriber, CardTicketYear?.Year))
            ApartmentTicketRows.Add(t);
    }

    [RelayCommand]
    private void OpenContract()
    {
        if (SelectedSubscriber is null) return;
        Go(AppModule.Contract);
        SelectedContractRow = ContractRows.FirstOrDefault(r => r.Subscriber.Id == SelectedSubscriber.Id);
    }

    [RelayCommand]
    private void OpenCard(ContractRow? row)
    {
        if (row is not null) SelectedSubscriber = row.Subscriber;
        if (SelectedSubscriber is null) return;
        Go(AppModule.Subscriber);
    }

    [RelayCommand]
    private async Task SaveCard()
    {
        if (SelectedSubscriber is null) return;
        if (!CanEditCard)
        {
            Status = "Нет права править договор и примечание (нужно право правки лицевого счёта).";
            return;
        }
        if (!TryParseDate(EditContractDate, out var signed) || !TryParseDate(EditContractTerminated, out var ended))
        {
            Status = "Дата договора — в виде дд.мм.гггг или пусто.";
            return;
        }
        if (signed is not null && ended is not null && ended < signed)
        {
            Status = "Договор не может быть расторгнут раньше, чем заключён.";
            return;
        }

        var s = SelectedSubscriber;
        await Save(async () =>
        {
            await _store.SaveContractAsync(s.Id, new UpdateContractRequest(
                EditContractNumber.Trim(), signed, ended, EditContractHolder.Trim(), EditNotes.Trim()));
            _cardBoundId = null;
            await ReloadAsync();
            Status = $"Карточка {s.LastName}, кв. {s.Apartment}, сохранена.";
        });
    }

    [RelayCommand]
    private async Task SavePaymentWarning()
    {
        if (!CanEditSettings)
        {
            Status = "Порог подсветки меняют администратор и начальник АДС.";
            return;
        }
        var months = (int)(EditWarningMonths ?? 0);
        if (!PaymentAge.IsValidThreshold(months))
        {
            Status = $"Порог — от {PaymentAge.MinWarningMonths} до {PaymentAge.MaxWarningMonths} месяцев.";
            return;
        }
        await Save(async () =>
        {
            await _store.SetPaymentWarningMonthsAsync(months);
            PaymentWarningMonths = months;
            Status = $"Жёлтая подсветка: без оплаты {PaymentAge.MonthsText(months)} и больше.";
        });
    }

    public static IReadOnlyList<string> TariffScopes { get; } = ["всем абонентам", "этому дому", "этой квартире"];

    [ObservableProperty] private string _tariffFrom = "";
    [ObservableProperty] private string _newTariffValue = "";
    [ObservableProperty] private string _tariffScope = TariffScopes[0];

    /// <summary>Новый тариф неоплаченным месяцам с выбранного: всем, дому карточки или её квартире.</summary>
    [RelayCommand]
    private async Task ChangeTariff()
    {
        if (!CanEditSettings || SelectedSubscriber is not { } s) return;
        if (!DateOnly.TryParseExact("01." + TariffFrom.Trim(), ["dd.MM.yyyy", "dd.M.yyyy"], Ru, DateTimeStyles.None, out var from))
        {
            Status = "Месяц, с которого действует тариф, — в виде мм.гггг.";
            return;
        }
        if (!decimal.TryParse(NewTariffValue.Trim(), NumberStyles.Number, Ru, out var tariff))
        {
            Status = "Тариф — сумма в рублях за месяц, например 100 или 100,50.";
            return;
        }
        var scope = Array.IndexOf([.. TariffScopes], TariffScope);
        await Save(async () =>
        {
            var months = await _store.ChangeTariffAsync(new TariffChangeRequest(from.Year, from.Month, tariff,
                scope == 1 ? s.HouseId : null, scope == 2 ? s.Id : null));
            await ReloadAsync();
            Status = $"Тариф {tariff:N2} ₽ с {from:MM.yyyy} {TariffScope}: изменено неоплаченных месяцев — {months}.";
        });
    }

    private static bool TryParseDate(string text, out DateOnly? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!DateOnly.TryParseExact(text.Trim(), ["dd.MM.yyyy", "d.M.yyyy"], Ru, DateTimeStyles.None, out var d)) return false;
        date = d;
        return true;
    }

    /// <summary>Дом абонента с организацией: у абонентов дом загружен без неё, она есть в списке домов.</summary>
    private House HouseOf(Subscriber s) => _houses.FirstOrDefault(h => h.Id == s.HouseId) ?? s.House;

    private static string MonthName(int month) => Ru.DateTimeFormat.MonthNames[month - 1];
}

public sealed record YearOption(int? Year, string Title);

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

/// <summary>Месяц в таблице оплат карточки: кто внёс и кто оплатил.</summary>
public sealed class CardPaymentRow(YearPayment p, string monthName)
{
    public string Month { get; } = monthName;
    public string Tariff { get; } = p.Tariff.ToString("N2");
    public string Amount { get; } = p.Amount?.ToString("N2") ?? "";
    public string PaidAt { get; } = p.PaidAt?.ToString("dd.MM.yyyy") ?? "";
    public string Source { get; } = p.Source ?? "";
    public string RecordedBy { get; } = p.RecordedBy ?? "";
    public string PayerName { get; } = p.PayerName ?? "";
}
