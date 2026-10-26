using System.Collections.ObjectModel;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>Словарь: добавление, правка и удаление улиц, домов, организаций, материалов и радиомехаников (администратор, главный инженер).</summary>
public sealed partial class PhonebookViewModel(MainViewModel shell) : ObservableObject
{
    /// <summary>Улицы, дома, материалы и радиомеханики — общие для всей программы.</summary>
    public MainViewModel Shell => shell;

    public ObservableCollection<Organization> Organizations { get; } = [];

    [ObservableProperty] private string _streetName = "";
    [ObservableProperty] private House? _editHouse;
    [ObservableProperty] private Organization? _selectedOrganization;
    [ObservableProperty] private Organization? _editOrganization;
    [ObservableProperty] private Material? _selectedMaterial;
    [ObservableProperty] private Material? _editMaterial;
    [ObservableProperty] private Mechanic? _selectedMechanic;
    [ObservableProperty] private Mechanic? _editMechanic;

    /// <summary>Запись, которую попросили удалить: удаляется вторым нажатием «Удалить».</summary>
    private object? _deleteTarget;

    public bool CanEditPhonebook => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Phonebook, out var level) && level == RoleCatalog.AccessLevel.Edit;

    public string EditHouseCaption => EditHouse is { Id: 0 } ? "Новый дом" : EditHouse is null ? "" : $"Дом {EditHouse.Number}";

    /// <summary>Сменился пользователь: права на правку словаря.</summary>
    internal void RefreshRights() => OnPropertyChanged(nameof(CanEditPhonebook));

    partial void OnEditHouseChanged(House? value) => OnPropertyChanged(nameof(EditHouseCaption));
    partial void OnSelectedOrganizationChanged(Organization? value) => EditOrganization = value;
    partial void OnSelectedMaterialChanged(Material? value) => EditMaterial = value;
    partial void OnSelectedMechanicChanged(Mechanic? value) => EditMechanic = value;

    [RelayCommand]
    private async Task AddStreet()
    {
        var name = StreetName.Trim();
        await shell.Save(async () =>
        {
            await shell.Store.SaveStreetAsync(new Street { Name = name });
            await shell.ReloadAsync();
            shell.SelectedStreet = shell.Streets.FirstOrDefault(s => s.Name == name);
            shell.Status = $"Улица {name} добавлена.";
        });
    }

    [RelayCommand]
    private async Task RenameStreet()
    {
        if (shell.SelectedStreet is not { } street) return;
        var name = StreetName.Trim();
        await shell.Save(async () =>
        {
            await shell.Store.SaveStreetAsync(new Street { Id = street.Id, Name = name });
            await shell.ReloadAsync();
            shell.SelectedStreet = shell.Streets.FirstOrDefault(s => s.Id == street.Id);
            shell.Status = $"Улица переименована: {name}.";
        });
    }

    [RelayCommand]
    private async Task DeleteStreet()
    {
        if (shell.SelectedStreet is not { } street || !ConfirmDelete(street, $"улицу {street.Name}")) return;
        await shell.Save(async () =>
        {
            await shell.Store.DeleteStreetAsync(street.Id);
            await shell.ReloadAsync();
            shell.Status = $"Улица {street.Name} удалена.";
        });
    }

    [RelayCommand]
    private void NewHouse()
    {
        if (shell.SelectedStreet is null) return;
        EditHouse = new House { StreetId = shell.SelectedStreet.Id, Street = shell.SelectedStreet, Serviced = true };
        shell.Status = "Заполните данные дома и нажмите «Сохранить».";
    }

    [RelayCommand]
    private async Task SaveHouseData()
    {
        if (EditHouse is not { } house) return;
        await shell.Save(async () =>
        {
            await shell.Store.SaveHouseAsync(house);
            await shell.ReloadAsync();
            shell.SelectedStreet = shell.Streets.FirstOrDefault(s => s.Id == house.StreetId);
            shell.SelectedHouse = shell.HousesOnStreet.FirstOrDefault(h => h.Number == house.Number.Trim());
            shell.Status = $"Дом {house.Number.Trim()} сохранён.";
        });
    }

    [RelayCommand]
    private async Task DeleteHouse()
    {
        if (EditHouse is not { Id: > 0 } house || !ConfirmDelete(house, $"дом {house.AddressLine}")) return;
        await shell.Save(async () =>
        {
            await shell.Store.DeleteHouseAsync(house.Id);
            await shell.ReloadAsync();
            shell.SelectedStreet = shell.Streets.FirstOrDefault(s => s.Id == house.StreetId);
            shell.Status = $"Дом {house.AddressLine} удалён.";
        });
    }

    [RelayCommand]
    private void NewOrganization()
    {
        SelectedOrganization = null;
        EditOrganization = new Organization();
    }

    [RelayCommand]
    private async Task SaveOrganization()
    {
        if (EditOrganization is not { } org) return;
        var name = org.Name.Trim();
        await shell.Save(async () =>
        {
            await shell.Store.SaveOrganizationAsync(org);
            await shell.ReloadAsync();
            SelectedOrganization = Organizations.FirstOrDefault(o => o.Name == name && o.Branch == org.Branch.Trim());
            shell.Status = $"Организация «{name}» сохранена.";
        });
    }

    [RelayCommand]
    private async Task DeleteOrganization()
    {
        if (EditOrganization is not { Id: > 0 } org || !ConfirmDelete(org, $"организацию «{org.Name}»")) return;
        await shell.Save(async () =>
        {
            await shell.Store.DeleteOrganizationAsync(org.Id);
            await shell.ReloadAsync();
            shell.Status = $"Организация «{org.Name}» удалена.";
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
        await shell.Save(async () =>
        {
            await shell.Store.SaveMaterialAsync(material);
            await shell.ReloadAsync();
            SelectedMaterial = shell.Materials.FirstOrDefault(m => m.Name == material.Name.Trim());
            shell.Status = $"Материал «{material.Name.Trim()}» сохранён.";
        });
    }

    [RelayCommand]
    private async Task DeleteMaterial()
    {
        if (EditMaterial is not { Id: > 0 } material || !ConfirmDelete(material, $"материал «{material.Name}»")) return;
        await shell.Save(async () =>
        {
            await shell.Store.DeleteMaterialAsync(material.Id);
            await shell.ReloadAsync();
            shell.Status = $"Материал «{material.Name}» удалён.";
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
        await shell.Save(async () =>
        {
            await shell.Store.SaveMechanicAsync(mechanic);
            await shell.ReloadAsync();
            SelectedMechanic = shell.Mechanics.FirstOrDefault(m => m.FullName == mechanic.FullName.Trim());
            shell.Status = $"Радиомеханик {mechanic.FullName.Trim()} сохранён.";
        });
    }

    [RelayCommand]
    private async Task DeleteMechanic()
    {
        if (EditMechanic is not { Id: > 0 } mechanic || !ConfirmDelete(mechanic, $"радиомеханика {mechanic.FullName}")) return;
        await shell.Save(async () =>
        {
            await shell.Store.DeleteMechanicAsync(mechanic.Id);
            await shell.ReloadAsync();
            shell.Status = $"Радиомеханик {mechanic.FullName} удалён. В старых заявках его фамилия остаётся.";
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
        shell.Status = $"Удалить {what}? Нажмите «Удалить» ещё раз.";
        return false;
    }
}
