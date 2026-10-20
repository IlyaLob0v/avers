using System.Collections.ObjectModel;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>Словарь: добавление, правка и удаление улиц, домов, материалов и радиомехаников (администратор, главный инженер).</summary>
public sealed partial class MainViewModel
{
    public ObservableCollection<Organization> Organizations { get; } = [];

    [ObservableProperty] private string _streetName = "";
    [ObservableProperty] private House? _editHouse;
    [ObservableProperty] private Material? _selectedMaterial;
    [ObservableProperty] private Material? _editMaterial;
    [ObservableProperty] private Mechanic? _selectedMechanic;
    [ObservableProperty] private Mechanic? _editMechanic;

    /// <summary>Запись, которую попросили удалить: удаляется вторым нажатием «Удалить».</summary>
    private object? _deleteTarget;

    public bool CanEditPhonebook => User is { } u && RoleCatalog.Can(u.Role, AppModule.Phonebook, out var level) && level == RoleCatalog.AccessLevel.Edit;

    public string EditHouseCaption => EditHouse is { Id: 0 } ? "Новый дом" : EditHouse is null ? "" : $"Дом {EditHouse.Number}";

    partial void OnEditHouseChanged(House? value) => OnPropertyChanged(nameof(EditHouseCaption));
    partial void OnSelectedMaterialChanged(Material? value) => EditMaterial = value;
    partial void OnSelectedMechanicChanged(Mechanic? value) => EditMechanic = value;

    [RelayCommand]
    private async Task AddStreet()
    {
        var name = StreetName.Trim();
        await Save(async () =>
        {
            await _store.SaveStreetAsync(new Street { Name = name });
            await ReloadAsync();
            SelectedStreet = Streets.FirstOrDefault(s => s.Name == name);
            Status = $"Улица {name} добавлена.";
        });
    }

    [RelayCommand]
    private async Task RenameStreet()
    {
        if (SelectedStreet is not { } street) return;
        var name = StreetName.Trim();
        await Save(async () =>
        {
            await _store.SaveStreetAsync(new Street { Id = street.Id, Name = name });
            await ReloadAsync();
            SelectedStreet = Streets.FirstOrDefault(s => s.Id == street.Id);
            Status = $"Улица переименована: {name}.";
        });
    }

    [RelayCommand]
    private async Task DeleteStreet()
    {
        if (SelectedStreet is not { } street || !ConfirmDelete(street, $"улицу {street.Name}")) return;
        await Save(async () =>
        {
            await _store.DeleteStreetAsync(street.Id);
            await ReloadAsync();
            Status = $"Улица {street.Name} удалена.";
        });
    }

    [RelayCommand]
    private void NewHouse()
    {
        if (SelectedStreet is null) return;
        EditHouse = new House { StreetId = SelectedStreet.Id, Street = SelectedStreet, Serviced = true };
        Status = "Заполните данные дома и нажмите «Сохранить».";
    }

    [RelayCommand]
    private async Task SaveHouseData()
    {
        if (EditHouse is not { } house) return;
        await Save(async () =>
        {
            await _store.SaveHouseAsync(house);
            await ReloadAsync();
            SelectedStreet = Streets.FirstOrDefault(s => s.Id == house.StreetId);
            SelectedHouse = HousesOnStreet.FirstOrDefault(h => h.Number == house.Number.Trim());
            Status = $"Дом {house.Number.Trim()} сохранён.";
        });
    }

    [RelayCommand]
    private async Task DeleteHouse()
    {
        if (EditHouse is not { Id: > 0 } house || !ConfirmDelete(house, $"дом {house.AddressLine}")) return;
        await Save(async () =>
        {
            await _store.DeleteHouseAsync(house.Id);
            await ReloadAsync();
            SelectedStreet = Streets.FirstOrDefault(s => s.Id == house.StreetId);
            Status = $"Дом {house.AddressLine} удалён.";
        });
    }

    [RelayCommand]
    private void NewMaterial()
    {
        SelectedMaterial = null;
        EditMaterial = new Material();
    }

    [RelayCommand]
    private async Task SaveMaterial()
    {
        if (EditMaterial is not { } material) return;
        await Save(async () =>
        {
            await _store.SaveMaterialAsync(material);
            await ReloadAsync();
            SelectedMaterial = Materials.FirstOrDefault(m => m.Name == material.Name.Trim());
            Status = $"Материал «{material.Name.Trim()}» сохранён.";
        });
    }

    [RelayCommand]
    private async Task DeleteMaterial()
    {
        if (EditMaterial is not { Id: > 0 } material || !ConfirmDelete(material, $"материал «{material.Name}»")) return;
        await Save(async () =>
        {
            await _store.DeleteMaterialAsync(material.Id);
            await ReloadAsync();
            Status = $"Материал «{material.Name}» удалён.";
        });
    }

    [RelayCommand]
    private void NewMechanic()
    {
        SelectedMechanic = null;
        EditMechanic = new Mechanic();
    }

    [RelayCommand]
    private async Task SaveMechanic()
    {
        if (EditMechanic is not { } mechanic) return;
        await Save(async () =>
        {
            await _store.SaveMechanicAsync(mechanic);
            await ReloadAsync();
            SelectedMechanic = Mechanics.FirstOrDefault(m => m.FullName == mechanic.FullName.Trim());
            Status = $"Радиомеханик {mechanic.FullName.Trim()} сохранён.";
        });
    }

    [RelayCommand]
    private async Task DeleteMechanic()
    {
        if (EditMechanic is not { Id: > 0 } mechanic || !ConfirmDelete(mechanic, $"радиомеханика {mechanic.FullName}")) return;
        await Save(async () =>
        {
            await _store.DeleteMechanicAsync(mechanic.Id);
            await ReloadAsync();
            Status = $"Радиомеханик {mechanic.FullName} удалён. В старых заявках его фамилия остаётся.";
        });
    }

    private bool ConfirmDelete(object target, string what)
    {
        if (_deleteTarget == target)
        {
            _deleteTarget = null;
            return true;
        }
        _deleteTarget = target;
        Status = $"Удалить {what}? Нажмите «Удалить» ещё раз.";
        return false;
    }
}
