using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.ViewModels;
using Avers.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avers.Desktop.Tests;

/// <summary>Программа на общей базе: настоящий сервер Аверса в памяти процесса и несколько сотрудников.</summary>
public sealed class ServerModeTests : IAsyncDisposable
{
    private readonly SharedServer _server = new();

    [Fact]
    public async Task PaymentOfOneEmployee_IsSeenByAnother()
    {
        var cashier = await SignIn("orlov");
        var subscriber = cashier.SelectedSubscriber!;
        var paidBefore = subscriber.Payments.Count(p => p.Amount is not null);

        await cashier.RecordPaymentCommand.ExecuteAsync(null);
        var admin = await SignIn("admin");

        Assert.StartsWith("Внесена оплата", cashier.Status);
        var seen = admin.SelectedSubscriber!;
        Assert.Equal(subscriber.Id, seen.Id);
        Assert.Equal(paidBefore + 1, seen.Payments.Count(p => p.Amount is not null));
        Assert.Contains(seen.Payments, p => p.RecordedBy == "Орлов С.А.");
    }

    [Fact]
    public async Task Find_LoadsHouseOfFoundSubscriber()
    {
        var vm = await SignIn("orlov");
        var houseId = vm.SelectedHouse!.Id;
        var other = _server.WithDb(db => db.Subscribers.Include(s => s.House).First(s => s.HouseId != houseId));

        vm.SearchMode = SearchMode.Account;
        vm.FindQuery = other.Account;
        await vm.FindCommand.ExecuteAsync(null);

        Assert.Equal(other.Id, vm.SelectedSubscriber!.Id);
        Assert.Equal(other.HouseId, vm.SelectedHouse!.Id);
        Assert.Equal(other.House.StreetId, vm.SelectedStreet!.Id);
        Assert.NotEmpty(vm.SelectedSubscriber.Payments);
        Assert.All(vm.ContractRows, r => Assert.Equal(other.HouseId, r.Subscriber.HouseId));
    }

    [Fact]
    public async Task Find_EmptySurname_ShowsServerAnswer()
    {
        var vm = await SignIn("orlov");
        var before = vm.SelectedSubscriber!.Id;

        vm.SearchMode = SearchMode.Surname;
        vm.FindQuery = " ";
        await vm.FindCommand.ExecuteAsync(null);

        Assert.Equal("Введите фамилию.", vm.Status);
        Assert.Equal(before, vm.SelectedSubscriber!.Id);
    }

    [Fact]
    public async Task WrongPassword_ShowsServerAnswer()
    {
        var vm = App.CreateMainViewModel(new ServerStore(_server.CreateClient()));
        vm.LoginName = "admin";
        vm.Password = "wrong";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Null(vm.User);
        Assert.Equal("Неверный логин или пароль.", vm.LoginError);
        Assert.True(vm.IsShared);
        Assert.Empty(vm.Users);
    }

    [Fact]
    public async Task Reader_GetsNoSubscribers()
    {
        var vm = await SignIn("novikov");

        Assert.Equal(UserRole.Reader, vm.User!.Role);
        Assert.Null(vm.SelectedSubscriber);
        Assert.Empty(vm.Debtors);
    }

    private async Task<MainViewModel> SignIn(string login)
    {
        var vm = App.CreateMainViewModel(new ServerStore(_server.CreateClient()));
        vm.LoginName = login;
        vm.Password = "avers";
        await vm.LoginCommand.ExecuteAsync(null);
        Assert.Null(vm.LoginError);
        return vm;
    }

    public async ValueTask DisposeAsync() => await _server.DisposeAsync();

    private sealed class SharedServer : WebApplicationFactory<Program>
    {
        private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"avers-shared-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Avers", $"Data Source={_databaseFile};Pooling=False");
            builder.UseSetting("Avers:Database:Provider", "Sqlite");
            builder.UseSetting("Avers:Database:EnsureCreated", "true");
            builder.UseSetting("Avers:Database:SeedDemo", "true");
        }

        public T WithDb<T>(Func<AversDbContext, T> action)
        {
            using var scope = Services.CreateScope();
            return action(scope.ServiceProvider.GetRequiredService<AversDbContext>());
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            File.Delete(_databaseFile);
        }
    }
}
