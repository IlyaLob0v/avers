using Avers.Desktop.Data;
using Avers.Desktop.Printing;
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
    [ObservableProperty] private string _caseNumber = "";
    [ObservableProperty] private string _caseOrder = "";
    [ObservableProperty] private string _caseDate = "";
    [ObservableProperty] private string _caseCourt = "";
    [ObservableProperty] private string _caseAmount = "";
    [ObservableProperty] private string _caseStatus = "";

    public bool CanEditCourtCases => User is { } u && RoleCatalog.Can(u.Role, AppModule.Legal, out var level) && level == RoleCatalog.AccessLevel.Edit;

    /// <summary>Последнее дело выбранного должника в полях формы; нет дела — пустая форма для нового.</summary>
    private void BindCourtCase()
    {
        var c = SelectedDebtor?.CourtCase;
        _caseId = c?.Id ?? 0;
        CaseNumber = c?.DelaAversId ?? "";
        CaseOrder = c?.OrderNumber ?? "";
        CaseDate = c?.OrderDate.ToString("dd.MM.yyyy") ?? "";
        CaseCourt = c?.Court ?? "";
        CaseAmount = c?.Amount.ToString("0.00", Ru) ?? SelectedDebtor?.Debt.ToString("0.00", Ru) ?? "";
        CaseStatus = c?.Status ?? "";
    }

    [RelayCommand]
    private void NewCourtCase()
    {
        if (SelectedDebtor is not { } d) return;
        _caseId = 0;
        CaseNumber = CaseOrder = CaseCourt = CaseStatus = "";
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
        await Save(async () =>
        {
            await _store.SaveCourtCaseAsync(d.SubscriberId, new CourtCase
            {
                Id = _caseId,
                DelaAversId = CaseNumber,
                OrderNumber = CaseOrder,
                OrderDate = date.Value,
                Court = CaseCourt,
                Amount = amount,
                Status = CaseStatus
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
        if (SelectedDebtor is not { } d) { Status = "Выберите должника."; return; }
        var date = PenaltyDate is { } chosen ? DateOnly.FromDateTime(chosen) : _today;
        if (await LoadDebtorAsync(d) is not { } s) return;
        var (header, title, body) = CourtOrderApplication.Text(s, date);
        OpenWord(PrintPath("заявление-на-приказ", d.Account, ".docx"), $"заявление на приказ {d.FullName}",
            path => WordForms.CourtOrder(header, title, body, path));
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
}
