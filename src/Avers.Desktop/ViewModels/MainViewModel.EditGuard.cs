using Avers.Contracts;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private Func<Task>? _pendingEditTransition;
    private bool _allowEditTransition;
    private House? _houseEditsOf;
    private HouseEditSnapshot? _houseEditBaseline;
    private CardEditSnapshot? _cardEditBaseline;
    [ObservableProperty] private bool _hasPendingEditTransition;
    [ObservableProperty] private bool _isResolvingEditTransition;

    private sealed record HouseEditSnapshot(HousePassportRequest Values, string FaultFix);
    private sealed record CardEditSnapshot(
        string Number, string Signed, string Ended, string Holder, string Notes, string Benefit,
        bool IsLegalEntity, string Inn, string Kpp, string Ogrn, string LegalAddress, string Director);

    public bool CanEditHousePassport => User is { } user
        && RoleCatalog.Can(user.Role, AppModule.Anten, out var level) && level == RoleCatalog.AccessLevel.Edit;

    private bool HasHouseEdits => _houseEditBaseline is not null && SelectedHouse == _houseEditsOf
        && CaptureHouseEdits() != _houseEditBaseline;
    private bool HasCardEdits => SelectedSubscriber is not null && _cardEditBaseline is not null
        && CaptureCardEdits() != _cardEditBaseline;
    private bool HasPaymentInput => !string.IsNullOrWhiteSpace(PaymentAmount);

    public bool CanUseWorkspace => !IsLoadingSubscriber && !HasPendingEditTransition;
    public bool CanSavePendingEdits => !HasPaymentInput;
    public string PendingEditMessage => HasPaymentInput
        ? "В поле оплаты осталась введённая сумма. Можно остаться либо очистить ввод и несохранённые правки перед переходом. Результат внесения оплаты указан ниже; переход сам по себе не проводит оплату."
        : "Есть несохранённые изменения дома или карточки абонента. Сохраните их, отмените или останьтесь на текущем экране.";

    /// <summary>Вызывается до изменения экрана или записи; continuation повторяет весь переход.</summary>
    public bool TryLeaveEdits(Func<Task> continuation)
    {
        if (_allowEditTransition) return true;
        if (HasPendingEditTransition) return false;
        if (!HasHouseEdits && !HasCardEdits && !HasPaymentInput) return true;
        _pendingEditTransition = continuation;
        HasPendingEditTransition = true;
        return false;
    }

    private HouseEditSnapshot? CaptureHouseEdits()
    {
        if (SelectedHouse is not { } house) return null;
        var organization = house.Organization is { } o
            ? new HouseOrganizationRequest(o.Name, o.Branch, o.Address, o.Phones, o.HeadName, o.EngineerName) : null;
        return new HouseEditSnapshot(new HousePassportRequest(
            house.Antennas, house.AerialCables, house.Amplifiers, house.PowerSupplies, house.TechFloorKey,
            house.OtherNotes, house.OpenFault, house.OpenFaultReason, house.OpenFaultPlannedFix,
            organization), EditFaultFix);
    }

    private CardEditSnapshot CaptureCardEdits() => new(
        EditContractNumber, EditContractDate, EditContractTerminated, EditContractHolder, EditNotes, EditBenefit,
        EditIsLegalEntity, EditInn, EditKpp, EditOgrn, EditLegalAddress, EditDirector);

    private void BindHouseEditBaseline()
    {
        _houseEditsOf = SelectedHouse;
        _houseEditBaseline = CaptureHouseEdits();
    }

    private void DiscardHouseEdits()
    {
        if (_houseEditsOf is not { } house || _houseEditBaseline is not { } baseline) return;
        var saved = baseline.Values;
        house.Antennas = saved.Antennas ?? "";
        house.AerialCables = saved.AerialCables ?? "";
        house.Amplifiers = saved.Amplifiers ?? "";
        house.PowerSupplies = saved.PowerSupplies ?? "";
        house.TechFloorKey = saved.TechFloorKey ?? "";
        house.OtherNotes = saved.OtherNotes ?? "";
        house.OpenFault = saved.OpenFault;
        house.OpenFaultReason = saved.OpenFaultReason;
        house.OpenFaultPlannedFix = saved.OpenFaultPlannedFix;
        if (house.Organization is { } organization && saved.Organization is { } o)
        {
            organization.Name = o.Name ?? "";
            organization.Branch = o.Branch ?? "";
            organization.Address = o.Address ?? "";
            organization.Phones = o.Phones ?? "";
            organization.HeadName = o.HeadName ?? "";
            organization.EngineerName = o.EngineerName ?? "";
        }
        EditFaultFix = baseline.FaultFix;
        OnPropertyChanged(nameof(SelectedHouse));
        OnPropertyChanged(nameof(FilteredHouses));
        RefreshDerived();
    }

    [RelayCommand]
    private void StayWithEdits()
    {
        if (IsResolvingEditTransition) return;
        _pendingEditTransition = null;
        HasPendingEditTransition = false;
    }

    [RelayCommand]
    private async Task DiscardEditsAndContinue()
    {
        if (IsResolvingEditTransition) return;
        if (HasHouseEdits) DiscardHouseEdits();
        if (HasCardEdits) BindCardEditors();
        PaymentAmount = "";
        await ContinueEditTransition();
    }

    [RelayCommand]
    private async Task SaveEditsAndContinue()
    {
        if (IsResolvingEditTransition || _pendingEditTransition is null || HasPaymentInput) return;
        IsResolvingEditTransition = true;
        try
        {
            // Перезагрузка дома сохраняет редакторы текущей карточки с тем же Id.
            if (HasHouseEdits && !await SaveHouseEdits()) return;
            if (HasCardEdits && !await SaveCardEdits()) return;
            await ContinueEditTransition();
        }
        finally { IsResolvingEditTransition = false; }
    }

    private async Task ContinueEditTransition()
    {
        var continuation = _pendingEditTransition;
        if (continuation is null) return;
        _pendingEditTransition = null;
        _allowEditTransition = true;
        try { await continuation(); }
        finally
        {
            _allowEditTransition = false;
            HasPendingEditTransition = false;
        }
    }

    partial void OnHasPendingEditTransitionChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUseWorkspace));
        OnPropertyChanged(nameof(CanSavePendingEdits));
        OnPropertyChanged(nameof(PendingEditMessage));
    }
    partial void OnIsLoadingSubscriberChanged(bool value) => OnPropertyChanged(nameof(CanUseWorkspace));
}
