using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Data;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class WorkflowNavigationTests
{
    [AvaloniaFact]
    public async Task ContractSelection_DoesNotReplaceEditedCardBeforeUserDecides()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        var registry = app.Vm.ContractsScreen;
        var original = registry.SelectedContractRow!;
        var other = registry.ContractRows.First(row => row.Subscriber.Id != original.Subscriber.Id);
        app.Vm.EditNotes = "Несохранённое примечание";

        registry.SelectedContractRow = other;

        Assert.Equal(original.Subscriber.Id, app.Vm.SelectedSubscriber?.Id);
        Assert.Equal(original.Subscriber.Id, registry.SelectedContractRow?.Subscriber.Id);
        Assert.True(app.Vm.HasPendingEditTransition);
        await app.Vm.DiscardEditsAndContinueCommand.ExecuteAsync(null);
        Assert.Equal(other.Subscriber.Id, app.Vm.SelectedSubscriber?.Id);
        Assert.Equal(other.Subscriber.Id, registry.SelectedContractRow?.Subscriber.Id);
    }

    [AvaloniaFact]
    public void HouseRegistry_OpenRequiresSelectedRow()
    {
        using var app = new DesktopApp(selectRecord: false);
        app.Vm.OpenModuleCommand.Execute(AppModule.Anten);

        Assert.False(app.Vm.OpenHouseCommand.CanExecute(null));
        app.Vm.SelectedHouseResult = app.Vm.FilteredHouses.First();
        Assert.True(app.Vm.OpenHouseCommand.CanExecute(null));
        app.Vm.HouseFilter = "Нет такого адреса";
        Assert.False(app.Vm.OpenHouseCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task TicketHouse_BackRestoresOriginalHouseAndJournalSelection()
    {
        using var app = new DesktopApp();
        var houseId = app.Vm.SelectedHouse!.Id;
        var subscriberId = app.Vm.SelectedSubscriber!.Id;
        app.Go(AppModule.Disp);
        var journal = app.Vm.TicketsScreen;
        journal.OnlyOpenTickets = true;
        journal.SelectedTicket = journal.Tickets.Single(ticket => ticket.Number == 1000);
        var ticketId = journal.SelectedTicket.Id;

        await journal.OpenTicketHouseCommand.ExecuteAsync(null);
        app.Pump();
        Assert.Equal(AppModule.Anten, app.Vm.Module);
        Assert.NotEqual(houseId, app.Vm.SelectedHouse?.Id);
        journal.OnlyOpenTickets = false;
        journal.SelectedTicket = journal.Tickets.Single(ticket => ticket.Number == 994);

        await app.Vm.BackCommand.ExecuteAsync(null);
        app.Pump();

        Assert.Equal(AppModule.Disp, app.Vm.Module);
        Assert.Equal(houseId, app.Vm.SelectedHouse?.Id);
        Assert.Equal(subscriberId, app.Vm.SelectedSubscriber?.Id);
        Assert.True(journal.OnlyOpenTickets);
        Assert.Equal(ticketId, journal.SelectedTicket?.Id);
        Assert.Equal(ticketId, journal.JournalSelectedTicket?.Id);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TicketSubscriberOrContract_BackRestoresOriginalSubscriber(bool openContract)
    {
        using var app = new DesktopApp();
        var subscriberId = app.Vm.SelectedSubscriber!.Id;
        var houseId = app.Vm.SelectedHouse!.Id;
        app.Go(AppModule.Disp);
        var journal = app.Vm.TicketsScreen;
        journal.SelectedTicket = journal.Tickets.Single(ticket => ticket.Number == 1000);
        var ticketId = journal.SelectedTicket.Id;

        if (openContract) await journal.OpenTicketContractCommand.ExecuteAsync(null);
        else await journal.OpenTicketSubscriberCommand.ExecuteAsync(null);
        app.Pump();
        Assert.Equal(openContract ? AppModule.Contract : AppModule.Subscriber, app.Vm.Module);
        Assert.NotEqual(subscriberId, app.Vm.SelectedSubscriber?.Id);

        await app.Vm.BackCommand.ExecuteAsync(null);
        app.Pump();

        Assert.Equal(AppModule.Disp, app.Vm.Module);
        Assert.Equal(houseId, app.Vm.SelectedHouse?.Id);
        Assert.Equal(subscriberId, app.Vm.SelectedSubscriber?.Id);
        Assert.Equal(ticketId, journal.SelectedTicket?.Id);
    }

    [AvaloniaFact]
    public async Task GlobalHousesEntry_ShowsRegistryAndOpeningRowSynchronizesStreet()
    {
        using var app = new DesktopApp();
        var entry = app.Vm.Navigation.Single(item => item.Module == AppModule.Anten);
        entry.Go.Execute(entry.Module);
        app.Pump();
        Assert.True(app.Window.GetVisualDescendants().OfType<Grid>().Single(control => control.Name == "HouseRegistry").IsEffectivelyVisible);
        Assert.False(app.Window.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "HousePassport").IsEffectivelyVisible);
        app.Vm.HouseFilter = "  волочаевская 19  ";
        Assert.Equal(new[] { "19", "19А", "19Д" }, app.Vm.FilteredHouses.Select(house => house.Number));
        var house = app.Vm.FilteredHouses.Single(item => item.Number == "19А");
        app.Vm.SelectedHouseResult = house;

        await app.Vm.OpenHouseCommand.ExecuteAsync(house);
        app.Pump();

        Assert.False(app.Vm.IsHouseList);
        Assert.Equal(house.Id, app.Vm.SelectedHouse?.Id);
        Assert.Equal(house.StreetId, app.Vm.SelectedStreet?.Id);
        Assert.True(app.Window.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "HousePassport").IsEffectivelyVisible);
        await app.Vm.BackCommand.ExecuteAsync(null);
        app.Pump();
        Assert.True(app.Vm.IsHouseList);
        Assert.Equal("  волочаевская 19  ", app.Vm.HouseFilter);
        Assert.Equal(house.Id, app.Vm.SelectedHouseResult?.Id);
    }

    [AvaloniaFact]
    public async Task SubscriberHouse_OpensOwnHouseAndBackReturnsToSameCard()
    {
        using var app = new DesktopApp();
        await FindStartSubscriber(app);
        app.Go(AppModule.Subscriber);
        var subscriberId = app.Vm.SelectedSubscriber!.Id;
        var houseId = app.Vm.SelectedSubscriber.HouseId;

        await app.Vm.OpenSubscriberHouseCommand.ExecuteAsync(null);
        app.Pump();

        Assert.Equal(AppModule.Anten, app.Vm.Module);
        Assert.False(app.Vm.IsHouseList);
        Assert.Equal(houseId, app.Vm.SelectedHouse?.Id);
        await app.Vm.BackCommand.ExecuteAsync(null);
        Assert.Equal(AppModule.Subscriber, app.Vm.Module);
        Assert.Equal(subscriberId, app.Vm.SelectedSubscriber?.Id);
    }

    [AvaloniaFact]
    public async Task Lawyer_CannotOpenSubscriberTechnicalHouse()
    {
        using var app = new DesktopApp("lawyer");
        await FindStartSubscriber(app);
        app.Go(AppModule.Subscriber);

        Assert.False(app.Vm.CanOpenSubscriberHouse);
        Assert.False(app.Vm.OpenSubscriberHouseCommand.CanExecute(null));
        await app.Vm.OpenSubscriberHouseCommand.ExecuteAsync(null);

        Assert.Equal(AppModule.Subscriber, app.Vm.Module);
        Assert.DoesNotContain(app.Vm.NavigationGroups.SelectMany(group => group.Items), item => item.Module == AppModule.Anten);
        Assert.All(app.Vm.NavigationGroups, group => Assert.NotEmpty(group.Items));
    }

    [AvaloniaFact]
    public async Task ContextualSubscribersEntry_KeepsCardButSearchButtonClearsIt()
    {
        using var app = new DesktopApp();
        await FindStartSubscriber(app);
        app.Go(AppModule.Subscriber);
        var subscriberId = app.Vm.SelectedSubscriber!.Id;

        app.Go(AppModule.Abon);
        Assert.Equal(subscriberId, app.Vm.SelectedSubscriber?.Id);
        app.Vm.ShowSubscriberSearchCommand.Execute(null);

        Assert.Null(app.Vm.SelectedSubscriber);
        Assert.Equal(AppModule.Abon, app.Vm.Module);
        Assert.Equal(DemoSeeder.StartAccount, app.Vm.FindQuery);
    }

    [AvaloniaFact]
    public async Task GlobalSubscribersEntry_StartsSearchInsteadOfKeepingPreviousCard()
    {
        using var app = new DesktopApp();
        await FindStartSubscriber(app);
        app.Go(AppModule.Subscriber);
        var entry = app.Vm.Navigation.Single(item => item.Module == AppModule.Abon);

        entry.Go.Execute(entry.Module);
        app.Pump();

        Assert.Equal(AppModule.Abon, app.Vm.Module);
        Assert.Null(app.Vm.SelectedSubscriber);
        Assert.Equal(DemoSeeder.StartAccount, app.Vm.FindQuery);
    }

    [AvaloniaFact]
    public async Task BackFromHouse_RestoresSubscriberAndSearchBeforeChangingHouse()
    {
        using var app = new DesktopApp();
        await FindStartSubscriber(app);
        var subscriberId = app.Vm.SelectedSubscriber!.Id;
        var houseId = app.Vm.SelectedHouse!.Id;
        app.Go(AppModule.Subscriber);
        app.Go(AppModule.Anten);
        app.Vm.SelectedHouse = app.Vm.HousesOnStreet.First(house => house.Id != houseId);
        app.Vm.FindQuery = "Другой запрос";
        app.Pump();

        await app.Vm.BackCommand.ExecuteAsync(null);
        app.Pump();

        Assert.Equal(AppModule.Subscriber, app.Vm.Module);
        Assert.Equal(subscriberId, app.Vm.SelectedSubscriber?.Id);
        Assert.Equal(houseId, app.Vm.SelectedHouse?.Id);
        Assert.Equal(app.Vm.SelectedHouse!.StreetId, app.Vm.SelectedStreet?.Id);
        Assert.Equal(DemoSeeder.StartAccount, app.Vm.FindQuery);
    }

    private static async Task FindStartSubscriber(DesktopApp app)
    {
        app.Go(AppModule.Abon);
        app.Vm.SearchMode = SearchMode.Account;
        app.Vm.FindQuery = DemoSeeder.StartAccount;
        await app.Vm.FindCommand.ExecuteAsync(null);
        app.Pump();
        Assert.NotNull(app.Vm.SelectedSubscriber);
    }
}
