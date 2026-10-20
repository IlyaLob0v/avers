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

        app.Vm.NewIntercomPanelCommand.Execute(null);
        app.Pump();
        Assert.Equal(0, app.Vm.Intercom?.Id);
        app.Vm.Intercom!.PanelType = "Метаком МК-20";
        await app.Vm.SaveIntercomPanelCommand.ExecuteAsync(null);

        Assert.Equal(count + 1, app.Vm.SelectedHouse!.Intercoms.Count);
        Assert.Equal("Метаком МК-20", app.Vm.Intercom?.PanelType);
        Assert.Same(app.Vm.Intercom, app.Vm.SelectedIntercomPanel);
    }

    [AvaloniaFact]
    public async Task AcceptThenClose_AndPrint()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Domofo);
        var count = app.Vm.IntercomTickets.Count;

        app.Vm.IntercomEntrance = "2";
        app.Vm.IntercomFault = "Не работает трубка кв. 70";
        app.Vm.IntercomMechanic = app.Vm.Mechanics[0];
        await app.Vm.AcceptIntercomTicketCommand.ExecuteAsync(null);

        Assert.Equal(count + 1, app.Vm.IntercomTickets.Count);
        app.Vm.SelectedIntercomTicket = app.Vm.IntercomTickets.Single(t => t.Fault == "Не работает трубка кв. 70");
        Assert.Equal(app.Vm.Mechanics[0].FullName, app.Vm.SelectedIntercomTicket.Mechanic);
        await app.Vm.CloseIntercomTicketCommand.ExecuteAsync(null);
        Assert.True(app.Vm.IntercomTickets.Single(t => t.Fault == "Не работает трубка кв. 70").Done);

        string? opened = null;
        app.Vm.OpenFile = path => opened = path;
        app.Vm.PrintIntercomCommand.Execute(null);
        Assert.True(new FileInfo(opened!).Length > 0);
    }
}
