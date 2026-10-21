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

    [ObservableProperty] private bool _packageDebtNote = true;
    [ObservableProperty] private bool _packageHistory = true;

    [RelayCommand]
    private void PrintClaim()
    {
        if (SelectedDebtor is not { } d) { Status = "Выберите должника."; return; }
        OpenPdf(PrintPath("уведомление", d.Account), $"досудебное уведомление {d.FullName}", path => PdfForms.Claim(ClaimText, path));
    }

    /// <summary>Пакет для суда по выбранному должнику: его оплаты грузятся вместе с домом.</summary>
    [RelayCommand]
    private async Task ExportCourtPackage()
    {
        if (SelectedDebtor is not { } d) { Status = "Выберите должника."; return; }
        if (!PackageDebtNote && !PackageHistory) { Status = "Отметьте, что положить в пакет."; return; }
        Subscriber s;
        try
        {
            s = (await _store.LoadHouseAsync(_houses.First(h => h.Id == d.HouseId))).First(x => x.Id == d.SubscriberId);
        }
        catch (StoreException e)
        {
            Status = e.Message;
            return;
        }
        OpenPdf(PrintPath("пакет-для-суда", d.Account), $"пакет для суда {d.FullName}",
            path => PdfForms.CourtPackage(s, _today, PackageDebtNote, PackageHistory, path));
    }
}
