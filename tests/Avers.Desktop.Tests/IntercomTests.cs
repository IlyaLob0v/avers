using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class IntercomTests
{
    [AvaloniaFact]
    public void EmptyHouse_ExplainsHowToAddPanelAndCannotSaveNothing()
    {
        using var app = new DesktopApp();
        app.Vm.SelectedHouse = app.Vm.HousesOnStreet.First(house => house.Intercoms.Count == 0);
        app.Go(AppModule.Domofo);

        Assert.False(app.Vm.IntercomScreen.SaveIntercomPanelCommand.CanExecute(null));
        Assert.Contains(app.Window.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == "В этом доме ещё нет панелей домофона.");
    }

    [AvaloniaFact]
    public async Task PanelDraft_StayAndDiscardProtectChangesBeforeLeaving()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Domofo);
        var before = app.Vm.SelectedHouse!.Intercoms.Count;
        app.Vm.IntercomScreen.NewIntercomPanelCommand.Execute(null);
        app.Vm.IntercomScreen.Intercom!.PanelType = "Черновик панели";

        app.Vm.OpenModuleCommand.Execute(AppModule.Ppr);
        Assert.True(app.Vm.HasPendingEditTransition);
        Assert.Equal(AppModule.Domofo, app.Vm.Module);
        app.Vm.StayWithEditsCommand.Execute(null);
        Assert.Equal("Черновик панели", app.Vm.IntercomScreen.Intercom.PanelType);

        app.Vm.OpenModuleCommand.Execute(AppModule.Ppr);
        await app.Vm.DiscardEditsAndContinueCommand.ExecuteAsync(null);
        Assert.Equal(AppModule.Ppr, app.Vm.Module);
        Assert.Equal(before, app.Vm.SelectedHouse!.Intercoms.Count);
    }

    [AvaloniaFact]
    public async Task PanelDraft_HouseSelectorWaitsForDiscardBeforeChangingContext()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Domofo);
        var originalHouse = app.Vm.SelectedHouse!;
        var nextHouse = app.Vm.HousesOnStreet.First(house => house.Id != originalHouse.Id);
        app.Vm.IntercomScreen.NewIntercomPanelCommand.Execute(null);
        app.Vm.IntercomScreen.Intercom!.PanelType = "Черновик перед сменой дома";
        var selector = app.Window.GetVisualDescendants().OfType<ComboBox>()
            .Single(box => box.IsEffectivelyVisible && ReferenceEquals(box.ItemsSource, app.Vm.HousesOnStreet));

        selector.SelectedItem = nextHouse;
        app.Pump();
        Assert.True(app.Vm.HasPendingEditTransition);
        Assert.Equal(originalHouse.Id, app.Vm.SelectedHouse!.Id);
        Assert.Equal("Черновик перед сменой дома", app.Vm.IntercomScreen.Intercom.PanelType);

        await app.Vm.DiscardEditsAndContinueCommand.ExecuteAsync(null);
        Assert.Equal(nextHouse.Id, app.Vm.SelectedHouse!.Id);
        Assert.DoesNotContain(originalHouse.Intercoms, panel => panel.PanelType == "Черновик перед сменой дома");
    }

    [AvaloniaFact]
    public async Task PanelDraft_SaveBeforeLeavingPersistsAndInvalidPanelStays()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Domofo);
        app.Vm.IntercomScreen.NewIntercomPanelCommand.Execute(null);
        app.Vm.IntercomScreen.Intercom!.Entrance = 0;
        app.Vm.IntercomScreen.Intercom.PanelType = "Новая панель";
        app.Vm.OpenModuleCommand.Execute(AppModule.Ppr);

        await app.Vm.SaveEditsAndContinueCommand.ExecuteAsync(null);
        Assert.Equal(AppModule.Domofo, app.Vm.Module);
        Assert.True(app.Vm.HasPendingEditTransition);
        app.Vm.IntercomScreen.Intercom.Entrance = 5;
        await app.Vm.SaveEditsAndContinueCommand.ExecuteAsync(null);
        Assert.Equal(AppModule.Ppr, app.Vm.Module);
        Assert.Contains(app.Vm.SelectedHouse!.Intercoms, panel => panel.Entrance == 5 && panel.PanelType == "Новая панель");
        app.Vm.OpenModuleCommand.Execute(AppModule.Domofo);
        Assert.False(app.Vm.HasPendingEditTransition);
        Assert.Equal(AppModule.Domofo, app.Vm.Module);
    }

    [AvaloniaFact]
    public void TicketHistory_ClosedRowsRequireExplicitFilterAndCannotBeClosedAgain()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Domofo);
        Assert.NotEmpty(app.Vm.IntercomScreen.IntercomTickets);
        var table = app.Window.GetVisualDescendants().OfType<DataGrid>().Single(grid => grid.IsEffectivelyVisible);
        Assert.Empty(table.ItemsSource!.Cast<IntercomTicket>());
        Assert.False(app.Vm.IntercomScreen.CloseIntercomTicketCommand.CanExecute(null));

        var history = app.Window.GetVisualDescendants().OfType<CheckBox>()
            .Single(box => Equals(box.Content, "Показать выполненные"));
        history.IsChecked = true;
        app.Pump();
        Assert.Equal(app.Vm.IntercomScreen.IntercomTickets.Count, table.ItemsSource!.Cast<IntercomTicket>().Count());
        app.Vm.IntercomScreen.SelectedIntercomTicket = app.Vm.IntercomScreen.IntercomTickets.First();
        Assert.False(app.Vm.IntercomScreen.CloseIntercomTicketCommand.CanExecute(null));
    }

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
        Assert.Equal(app.Vm.IntercomScreen.Intercom?.Id, app.Vm.IntercomScreen.SelectedIntercomPanel?.Id);
        Assert.NotSame(app.Vm.IntercomScreen.Intercom, app.Vm.IntercomScreen.SelectedIntercomPanel);
        app.Go(AppModule.Ppr);
        Assert.False(app.Vm.HasPendingEditTransition);
        Assert.Equal(AppModule.Ppr, app.Vm.Module);
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
