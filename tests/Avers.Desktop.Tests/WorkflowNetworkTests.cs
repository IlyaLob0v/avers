using Avalonia.Headless.XUnit;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.ViewModels;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class WorkflowNetworkTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateNavigationReply_DoesNotRestorePreviousUsersJournal(bool back)
    {
        await using var server = new ServerModeTests.SharedServer();
        var houseId = server.WithDb(db => db.Houses.Single(house => house.Number == (back ? "19Д" : "18")).Id);
        using var delay = new ServerModeTests.ResponseDelayHandler(server.Server.CreateHandler(), $"/api/houses/{houseId}/subscribers");
        using var client = new HttpClient(delay) { BaseAddress = new Uri("http://localhost/") };
        var vm = App.CreateMainViewModel(new ServerStore(client));
        await SignIn(vm, "admin");
        vm.GoCommand.Execute(AppModule.Abon);
        vm.SearchMode = SearchMode.Account;
        vm.FindQuery = DemoSeeder.StartAccount;
        await vm.FindCommand.ExecuteAsync(null);
        vm.TicketsScreen.SelectedTicket = vm.TicketsScreen.Tickets.Single(ticket => ticket.Number == 1000);
        if (back)
        {
            vm.GoCommand.Execute(AppModule.Subscriber);
            vm.GoCommand.Execute(AppModule.Anten);
        }
        else vm.GoCommand.Execute(AppModule.Disp);
        delay.PauseNext();

        var navigation = back ? vm.BackCommand.ExecuteAsync(null) : vm.TicketsScreen.OpenTicketSubscriberCommand.ExecuteAsync(null);
        await delay.ResponseReady.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        vm.LogoutCommand.Execute(null);
        await SignIn(vm, "lawyer");
        Assert.Null(vm.TicketsScreen.SelectedTicket);
        delay.Release();
        await navigation;

        Assert.Equal(UserRole.Lawyer, vm.User?.Role);
        Assert.Equal(AppModule.Legal, vm.Module);
        Assert.Null(vm.SelectedSubscriber);
        Assert.Null(vm.TicketsScreen.SelectedTicket);
    }

    private static async Task SignIn(MainViewModel vm, string login)
    {
        vm.LoginName = login;
        vm.Password = "avers";
        await vm.LoginCommand.ExecuteAsync(null);
        Assert.Null(vm.LoginError);
    }
}
