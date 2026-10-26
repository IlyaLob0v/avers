using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class PhonebookTests
{
    [AvaloniaFact]
    public async Task Engineer_AddsStreetWithHouse()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Phonebook);
        Assert.True(app.Vm.PhonebookScreen.CanEditPhonebook);

        app.Vm.PhonebookScreen.StreetName = "Звёздная";
        await app.Vm.PhonebookScreen.AddStreetCommand.ExecuteAsync(null);
        Assert.Equal("Звёздная", app.Vm.SelectedStreet?.Name);
        Assert.Empty(app.Vm.HousesOnStreet);

        app.Vm.PhonebookScreen.NewHouseCommand.Execute(null);
        app.Vm.PhonebookScreen.EditHouse!.Number = "7А";
        app.Vm.PhonebookScreen.EditHouse.Floors = 5;
        await app.Vm.PhonebookScreen.SaveHouseDataCommand.ExecuteAsync(null);

        Assert.Equal("Дом 7А сохранён.", app.Vm.Status);
        Assert.Equal("Звёздная", app.Vm.SelectedStreet?.Name);
        Assert.Equal(("7А", 5), (app.Vm.SelectedHouse?.Number, app.Vm.SelectedHouse?.Floors));
    }

    [AvaloniaFact]
    public async Task DeleteMechanic_NeedsSecondClick()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Phonebook);
        var count = app.Vm.Mechanics.Count;
        app.Vm.PhonebookScreen.SelectedMechanic = app.Vm.Mechanics[0];

        await app.Vm.PhonebookScreen.DeleteMechanicCommand.ExecuteAsync(null);
        Assert.Equal(count, app.Vm.Mechanics.Count);
        await app.Vm.PhonebookScreen.DeleteMechanicCommand.ExecuteAsync(null);

        Assert.Equal(count - 1, app.Vm.Mechanics.Count);
    }

    [AvaloniaFact]
    public async Task Engineer_AddsOrganization_AndPicksItForHouse()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Phonebook);
        var screen = app.Vm.PhonebookScreen;

        screen.NewOrganizationCommand.Execute(null);
        screen.EditOrganization!.Name = "ТСЖ «Рассвет»";
        screen.EditOrganization.Phones = "21-00-00";
        await screen.SaveOrganizationCommand.ExecuteAsync(null);

        Assert.Equal("Организация «ТСЖ «Рассвет»» сохранена.", app.Vm.Status);
        var org = Assert.Single(screen.Organizations, o => o.Name == "ТСЖ «Рассвет»");
        Assert.Same(org, screen.SelectedOrganization);
        screen.EditHouse = app.Vm.SelectedHouse;
        screen.EditHouse!.OrganizationId = org.Id;
        await screen.SaveHouseDataCommand.ExecuteAsync(null);
        Assert.Equal("ТСЖ «Рассвет»", app.Vm.SelectedHouse!.Organization?.Name);
    }

    [AvaloniaFact]
    public async Task HouseWithSubscribers_StaysWithMessage()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Phonebook);
        var house = app.Vm.SelectedHouse!;

        await app.Vm.PhonebookScreen.DeleteHouseCommand.ExecuteAsync(null);
        await app.Vm.PhonebookScreen.DeleteHouseCommand.ExecuteAsync(null);

        Assert.StartsWith("У дома есть абоненты", app.Vm.Status);
        Assert.Contains(app.Vm.HousesOnStreet, h => h.Id == house.Id);
    }

    [AvaloniaFact]
    public void Dispatcher_SeesPhonebookReadOnly()
    {
        using var app = new DesktopApp("orlova");
        app.Go(AppModule.Phonebook);

        Assert.False(app.Vm.PhonebookScreen.CanEditPhonebook);
    }
}
