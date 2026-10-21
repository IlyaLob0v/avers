using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>Калькулятор обратного отсчёта на экране должников: этап, дата события, сколько дней осталось.</summary>
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
}
