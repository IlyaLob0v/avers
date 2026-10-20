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
        Assert.True(app.Vm.CanEditPhonebook);

        app.Vm.StreetName = "Звёздная";
        await app.Vm.AddStreetCommand.ExecuteAsync(null);
        Assert.Equal("Звёздная", app.Vm.SelectedStreet?.Name);
        Assert.Empty(app.Vm.HousesOnStreet);

        app.Vm.NewHouseCommand.Execute(null);
        app.Vm.EditHouse!.Number = "7А";
        app.Vm.EditHouse.Floors = 5;
        await app.Vm.SaveHouseDataCommand.ExecuteAsync(null);

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
        app.Vm.SelectedMechanic = app.Vm.Mechanics[0];

        await app.Vm.DeleteMechanicCommand.ExecuteAsync(null);
        Assert.Equal(count, app.Vm.Mechanics.Count);
        await app.Vm.DeleteMechanicCommand.ExecuteAsync(null);

        Assert.Equal(count - 1, app.Vm.Mechanics.Count);
    }

    [AvaloniaFact]
    public async Task HouseWithSubscribers_StaysWithMessage()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Phonebook);
        var house = app.Vm.SelectedHouse!;

        await app.Vm.DeleteHouseCommand.ExecuteAsync(null);
        await app.Vm.DeleteHouseCommand.ExecuteAsync(null);

        Assert.StartsWith("У дома есть абоненты", app.Vm.Status);
        Assert.Contains(app.Vm.HousesOnStreet, h => h.Id == house.Id);
    }

    [AvaloniaFact]
    public void Dispatcher_SeesPhonebookReadOnly()
    {
        using var app = new DesktopApp("orlova");
        app.Go(AppModule.Phonebook);

        Assert.False(app.Vm.CanEditPhonebook);
    }
}
