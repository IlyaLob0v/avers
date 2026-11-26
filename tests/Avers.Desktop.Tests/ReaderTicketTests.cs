using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.ViewModels;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class ReaderTicketTests
{
    [AvaloniaFact]
    public void SelectedHouse_ShowsItsOpenFaultsAndResponsible_InReadOnlyTable()
    {
        using var app = new DesktopApp("novikov");
        app.Go(AppModule.Reader);
        app.Pump();

        var grid = Assert.Single(app.Window.GetVisualDescendants().OfType<DataGrid>(),
            item => item.IsEffectivelyVisible && AutomationProperties.GetName(item) == "Открытые заявки выбранного дома");
        Assert.True(grid.IsReadOnly);
        Assert.NotEmpty(app.Vm.ReaderTickets);
        Assert.Contains(app.Vm.ReaderTickets, ticket => ticket.Number == DemoSeeder.StartTicket);
        Assert.All(app.Vm.ReaderTickets, ticket => Assert.False(string.IsNullOrWhiteSpace(ticket.Fault)));
        Assert.All(app.Vm.ReaderTickets, ticket => Assert.False(string.IsNullOrWhiteSpace(ticket.Responsible)));

        app.Vm.SelectedHouse = null;
        app.Pump();
        Assert.Empty(app.Vm.ReaderTickets);
        Assert.Contains("Выберите дом", app.Vm.ReaderTicketsStatus);
    }

    [AvaloniaFact]
    public async Task LateHouseTickets_DoNotReplaceAnotherHouseOrSurviveLogout()
    {
        await using var server = new ServerModeTests.SharedServer();
        var houseId = server.WithDb(db => db.Houses.Single(h => h.Number == "19Д").Id);
        using var delay = new ServerModeTests.ResponseDelayHandler(server.Server.CreateHandler(), $"/api/mobile/houses/{houseId}/tickets");
        using var client = new HttpClient(delay) { BaseAddress = new Uri("http://localhost/") };
        var vm = App.CreateMainViewModel(new ServerStore(client));
        vm.LoginName = "novikov";
        vm.Password = "avers";
        await vm.LoginCommand.ExecuteAsync(null);
        vm.SelectedStreet = vm.Streets.Single(street => street.Name == "Волочаевская");
        var first = vm.HousesOnStreet.Single(h => h.Id == houseId);
        vm.SelectedHouse = first;
        await vm.RefreshReaderTicketsCommand.ExecuteAsync(null);
        delay.PauseNext();
        var pending = vm.RefreshReaderTicketsCommand.ExecuteAsync(null);
        await delay.ResponseReady.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        vm.SelectedHouse = vm.HousesOnStreet.First(h => h.Id != houseId);
        vm.LogoutCommand.Execute(null);
        delay.Release();
        await pending;

        Assert.Null(vm.User);
        Assert.Empty(vm.ReaderTickets);
        Assert.False(vm.IsLoadingReaderTickets);
    }
}
