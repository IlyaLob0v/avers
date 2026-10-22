using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class IntercomTests
{
    [AvaloniaFact]
    public async Task Admin_AddsPanelForNextEntrance()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Domofo);
        var count = app.Vm.SelectedHouse!.Intercoms.Count;

        app.Vm.IntercomScreen.NewIntercomPanelCommand.Execute(null);
        app.Pump();
        Assert.Equal(0, app.Vm.IntercomScreen.Intercom?.Id);
        app.Vm.IntercomScreen.Intercom!.PanelType = "Метаком МК-20";
        await app.Vm.IntercomScreen.SaveIntercomPanelCommand.ExecuteAsync(null);

        Assert.Equal(count + 1, app.Vm.SelectedHouse!.Intercoms.Count);
        Assert.Equal("Метаком МК-20", app.Vm.IntercomScreen.Intercom?.PanelType);
        Assert.Same(app.Vm.IntercomScreen.Intercom, app.Vm.IntercomScreen.SelectedIntercomPanel);
    }

    [AvaloniaFact]
    public async Task AcceptThenClose_AndPrint()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Domofo);
        var count = app.Vm.IntercomScreen.IntercomTickets.Count;

        app.Vm.IntercomScreen.IntercomEntrance = "2";
        app.Vm.IntercomScreen.IntercomFault = "Не работает трубка кв. 70";
        app.Vm.IntercomScreen.IntercomMechanic = app.Vm.Mechanics[0];
        await app.Vm.IntercomScreen.AcceptIntercomTicketCommand.ExecuteAsync(null);

        Assert.Equal(count + 1, app.Vm.IntercomScreen.IntercomTickets.Count);
        app.Vm.IntercomScreen.SelectedIntercomTicket = app.Vm.IntercomScreen.IntercomTickets.Single(t => t.Fault == "Не работает трубка кв. 70");
        Assert.Equal(app.Vm.Mechanics[0].FullName, app.Vm.IntercomScreen.SelectedIntercomTicket.Mechanic);
        await app.Vm.IntercomScreen.CloseIntercomTicketCommand.ExecuteAsync(null);
        Assert.True(app.Vm.IntercomScreen.IntercomTickets.Single(t => t.Fault == "Не работает трубка кв. 70").Done);

        string? opened = null;
        app.Vm.OpenFile = path => opened = path;
        app.Vm.IntercomScreen.PrintIntercomCommand.Execute(null);
        Assert.True(new FileInfo(opened!).Length > 0);
    }
}
