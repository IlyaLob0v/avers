using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

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
}
