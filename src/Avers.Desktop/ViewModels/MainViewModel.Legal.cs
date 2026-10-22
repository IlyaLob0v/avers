using Avers.Desktop.Data;
using Avers.Desktop.Printing;
using Avers.Desktop.Reports;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Экран должников: калькулятор обратного отсчёта (этап, дата события, сколько дней осталось),
/// печать досудебного уведомления и пакета документов для суда.
/// </summary>
public sealed partial class MainViewModel
{
    public IReadOnlyList<LegalStage> LegalStages => LegalDeadline.Stages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegalRemaining))]
    private LegalStage _legalStage = LegalDeadline.Stages[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegalRemaining))]
    private DateTime? _legalStart;

    public string LegalRemaining => LegalStart is { } start
        ? LegalDeadline.Remaining(LegalDeadline.Due(LegalStage, DateOnly.FromDateTime(start)), _today)
        : "";

    private int _caseId;
    [ObservableProperty] private string _caseOrder = "";
    [ObservableProperty] private string _caseDate = "";
    [ObservableProperty] private string _caseCourt = "";
    [ObservableProperty] private string _caseAmount = "";
    [ObservableProperty] private string _caseStatus = "";
    [ObservableProperty] private string _caseDuty = "";
    [ObservableProperty] private string _caseWritTo = "";
    [ObservableProperty] private string _caseWritDate = "";
    [ObservableProperty] private string _caseReceipts = "";
    [ObservableProperty] private string _caseNotes = "";

    public bool CanEditCourtCases => User is { } u && RoleCatalog.Can(u.Role, AppModule.Legal, out var level) && level == RoleCatalog.AccessLevel.Edit;

    /// <summary>Последнее дело выбранного должника в полях формы; нет дела — пустая форма для нового.</summary>
    private void BindCourtCase()
    {
        var c = SelectedDebtor?.CourtCase;
        _caseId = c?.Id ?? 0;
        CaseOrder = c?.OrderNumber ?? "";
        CaseDate = c?.OrderDate.ToString("dd.MM.yyyy") ?? "";
        CaseCourt = c?.Court ?? "";
        CaseAmount = c?.Amount.ToString("0.00", Ru) ?? SelectedDebtor?.Debt.ToString("0.00", Ru) ?? "";
        CaseStatus = c?.Status ?? "";
        CaseDuty = c?.StateDuty.ToString("0.00", Ru) ?? "";
        CaseWritTo = c?.WritSentTo ?? "";
        CaseWritDate = c?.WritSentDate?.ToString("dd.MM.yyyy") ?? "";
        CaseReceipts = c?.Receipts ?? "";
        CaseNotes = c?.Notes ?? "";
    }

    [RelayCommand]
    private void NewCourtCase()
    {
        if (SelectedDebtor is not { } d) return;
        _caseId = 0;
        CaseOrder = CaseCourt = CaseStatus = CaseDuty = CaseWritTo = CaseWritDate = CaseReceipts = CaseNotes = "";
        CaseDate = _today.ToString("dd.MM.yyyy");
        CaseAmount = d.Debt.ToString("0.00", Ru);
        Status = $"Новое дело для {d.FullName}: заполните и нажмите «Сохранить дело».";
    }

    [RelayCommand]
    private async Task SaveCourtCase()
    {
        if (SelectedDebtor is not { } d || !CanEditCourtCases) return;
        if (!TryParseDate(CaseDate, out var date) || date is null)
        {
            Status = "Дата приказа — в виде дд.мм.гггг.";
            return;
        }
        if (!decimal.TryParse(CaseAmount.Trim(), System.Globalization.NumberStyles.Number, Ru, out var amount))
        {
            Status = "Сумма — в рублях, например 475 или 475,50.";
            return;
        }
        var duty = 0m;
        if (CaseDuty.Trim() != "" && !decimal.TryParse(CaseDuty.Trim(), System.Globalization.NumberStyles.Number, Ru, out duty))
        {
            Status = "Госпошлина — в рублях, например 200 или 200,50.";
            return;
        }
        if (!TryParseDate(CaseWritDate, out var writDate))
        {
            Status = "Дата направления листа — в виде дд.мм.гггг.";
            return;
        }
        await Save(async () =>
        {
            await _store.SaveCourtCaseAsync(d.SubscriberId, new CourtCase
            {
                Id = _caseId,
                OrderNumber = CaseOrder,
                OrderDate = date.Value,
                Court = CaseCourt,
                Amount = amount,
                Status = CaseStatus,
                StateDuty = duty,
                WritSentTo = CaseWritTo,
                WritSentDate = writDate,
                Receipts = CaseReceipts,
                Notes = CaseNotes
            });
            await LoadDebtsAsync();
            Status = $"Дело {d.FullName} сохранено.";
        });
    }

    [ObservableProperty] private bool _packageDebtNote = true;
    [ObservableProperty] private bool _packageHistory = true;

    public IReadOnlyList<ClaimVariant> ClaimVariants => ClaimNotice.Variants;

    [ObservableProperty] private ClaimVariant _claimVariant = ClaimNotice.Variants[0];

    partial void OnClaimVariantChanged(ClaimVariant value) => BuildClaim();

    /// <summary>Уведомление в Word: юристы правят текст и печатают оттуда.</summary>
    [RelayCommand]
    private void PrintClaim()
    {
        if (SelectedDebtor is not { } d) { Status = "Выберите должника."; return; }
        OpenWord(PrintPath("уведомление", d.Account, ".docx"), $"уведомление {d.FullName}",
            path => WordForms.Claim($"г. Омск, {d.Address}", ClaimText, path));
    }

    /// <summary>Пакет для суда по выбранному должнику: его оплаты грузятся вместе с домом.</summary>
    [RelayCommand]
    private async Task ExportCourtPackage()
    {
        if (SelectedDebtor is not { } d) { Status = "Выберите должника."; return; }
        if (!PackageDebtNote && !PackageHistory) { Status = "Отметьте, что положить в пакет."; return; }
        if (await LoadDebtorAsync(d) is not { } s) return;
        OpenPdf(PrintPath("пакет-для-суда", d.Account), $"пакет для суда {d.FullName}",
            path => PdfForms.CourtPackage(s, _today, PackageDebtNote, PackageHistory, path));
    }

    [ObservableProperty] private DateTime? _penaltyDate;

    /// <summary>Расчёт неустойки на дату заявления; по умолчанию — на сегодня.</summary>
    [RelayCommand]
    private async Task PrintPenalty()
    {
        if (SelectedDebtor is not { } d) { Status = "Выберите должника."; return; }
        var date = PenaltyDate is { } chosen ? DateOnly.FromDateTime(chosen) : _today;
        if (await LoadDebtorAsync(d) is not { } s) return;
        OpenPdf(PrintPath("неустойка", d.Account), $"расчёт неустойки {d.FullName}", path => PdfForms.Penalty(s, date, path));
    }

    /// <summary>Заявление о выдаче судебного приказа в Word: долг и неустойка на ту же дату, что и расчёт неустойки.</summary>
    [RelayCommand]
    private async Task PrintCourtOrder()
    {
        var date = PenaltyDate is { } chosen ? DateOnly.FromDateTime(chosen) : _today;
        await OpenApplication("заявление-на-приказ", "заявление на приказ", s => CourtOrderApplication.Text(s, date));
    }

    /// <summary>Заявление в ОСП в Word по последнему судебному делу должника.</summary>
    [RelayCommand]
    private Task PrintEnforcement() =>
        OpenApplication("заявление-в-осп", "заявление в ОСП", s => EnforcementApplication.Text(s, SelectedDebtor?.CourtCase));

    private async Task OpenApplication(string file, string title, Func<Subscriber, (string[] Header, string Title, string[] Body)> text)
    {
        if (SelectedDebtor is not { } d) { Status = "Выберите должника."; return; }
        if (await LoadDebtorAsync(d) is not { } s) return;
        var (header, heading, body) = text(s);
        OpenWord(PrintPath(file, d.Account, ".docx"), $"{title} {d.FullName}",
            path => WordForms.Application(header, heading, body, path));
    }

    /// <summary>Абонент с оплатами: они грузятся вместе с домом.</summary>
    private async Task<Subscriber?> LoadDebtorAsync(DebtorRow d)
    {
        try
        {
            return (await _store.LoadHouseAsync(_houses.First(h => h.Id == d.HouseId))).First(x => x.Id == d.SubscriberId);
        }
        catch (StoreException e)
        {
            Status = e.Message;
            return null;
        }
    }

    [RelayCommand]
    private void ExportDebtors() =>
        Export("должники", "отчёт по должникам", path => XlsxTable.Write(path,
            $"Должники на {_today:dd.MM.yyyy}",
            ["ФИО", "Адрес", "Лиц. счёт", "Долг, руб.", "Неоплачено мес.", "Последняя оплата", "Судебный приказ"],
            Debtors.Select(d => new object?[] { d.FullName, d.Address, d.Account, d.Debt, d.Unpaid, d.LastPay, d.Case })));
}
