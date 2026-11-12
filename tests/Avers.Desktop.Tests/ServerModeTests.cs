using System.Text.Json;
using Avers.Contracts;
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
    public async Task AccessLog_ShowsWhoOpenedSubscribers()
    {
        var cashier = await SignIn("orlov");
        var house = cashier.SelectedHouse!.AddressLine;
        var admin = await SignIn("admin");

        admin.ChangeLogScreen.ShowViews = true;
        await admin.ChangeLogScreen.LoadAsync();

        Assert.Contains(admin.ChangeLogScreen.ChangeLogRows,
            r => r.UserName == "Орлов С.А. (orlov)" && r.Action == "Абоненты дома" && r.Target == house);
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
        Assert.All(vm.ContractsScreen.ContractRows, r => Assert.Equal(other.HouseId, r.Subscriber.HouseId));
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
    public async Task Journal_ComesFromServerInParts_AcceptedTicketAppears()
    {
        var vm = await SignIn("sokolova");
        var screen = vm.TicketsScreen;

        Assert.NotEmpty(screen.Tickets);
        Assert.All(screen.Tickets, t => Assert.Equal(DemoSeeder.WorkDate.Year, t.Date.Year));
        Assert.All(screen.Tickets, t => Assert.Equal(t.HouseId, t.House.Id));
        Assert.Equal(DemoSeeder.StartTicket, screen.SelectedTicket!.Number);
        Assert.Equal("Иванова Мария Петровна", screen.TicketSubscriberName);
        Assert.Equal("Иванова М.П.", screen.SelectedTicket.Subscriber!.ShortName);
        Assert.True(screen.CanEditSelectedTicket);
        Assert.Equal(screen.Tickets.Count, screen.TicketMonths.Sum(m => int.TryParse(m.Counts.Split(" / ")[0], out var n) ? n : 0));

        screen.NewTicketFault = "Нет сигнала на 1-м канале";
        await screen.AcceptTicketCommand.ExecuteAsync(null);

        Assert.StartsWith("Принята заявка", vm.Status);
        Assert.Equal("Нет сигнала на 1-м канале", screen.SelectedTicket!.Fault);
        Assert.Contains(screen.Tickets, t => t.Id == screen.SelectedTicket.Id);
    }

    [Fact]
    public async Task Oek_DayComesFromServer_AcceptedTicketCounts()
    {
        var vm = await SignIn("sokolova");
        var screen = vm.TicketsScreen;
        var before = screen.OekWarning;

        screen.NewTicketFault = "Нет сигнала";
        await screen.AcceptTicketCommand.ExecuteAsync(null);

        // День ОЭК — сегодня; принятая заявка не выполнена, значит список скрыт и счёт другой.
        Assert.Equal(screen.SelectedTicket!.Date, DateOnly.FromDateTime(screen.OekDate!.Value));
        Assert.Contains("Список для ОЭК скрыт", screen.OekWarning);
        Assert.NotEqual(before, screen.OekWarning);
    }

    [Fact]
    public async Task Reader_GetsNoSubscribers()
    {
        var vm = await SignIn("novikov");

        Assert.Equal(UserRole.Reader, vm.User!.Role);
        Assert.Null(vm.SelectedSubscriber);
        Assert.Empty(vm.LegalScreen.Debtors);
    }

    [Fact]
    public async Task Payment_RecordedAndCancelledThroughServer()
    {
        var (store, user) = await Connect("admin");
        const int subscriberId = 1;

        var paid = await store.RecordPaymentAsync(subscriberId, Year, null, user);
        var saved = _server.WithDb(db => db.Payments.AsNoTracking().Single(p => p.Id == paid.Id));
        Assert.True(paid.IsPaid);
        Assert.Equal((paid.Month, paid.Tariff, "Касса"), (saved.Month, saved.Amount!.Value, saved.Source!));

        await store.CancelPaymentAsync(subscriberId, paid.Id);

        Assert.Null(_server.WithDb(db => db.Payments.AsNoTracking().Single(p => p.Id == paid.Id).Amount));
    }

    [Fact]
    public async Task CancelUnpaidMonth_ShowsServerReason()
    {
        var (store, user) = await Connect("admin");
        var paid = await store.RecordPaymentAsync(1, Year, null, user);
        await store.CancelPaymentAsync(1, paid.Id);

        var error = await Assert.ThrowsAsync<StoreException>(() => store.CancelPaymentAsync(1, paid.Id));

        Assert.Equal($"Месяц {paid.Month:00}.{Year} не оплачен.", error.Message);
    }

    [Fact]
    public async Task Debts_SameAsInDatabase()
    {
        var (store, _) = await Connect("admin");

        var debts = await store.LoadDebtsAsync();
        var expected = _server.WithDb(db => Debts.Load(db, store.Today));

        Assert.NotEmpty(debts.Debtors);
        Assert.Equal(Serialize(expected.Debtors), Serialize(debts.Debtors));
        Assert.Equal(expected.ByHouse, debts.ByHouse);
    }

    [Fact]
    public async Task AccountingReport_SameAsInDatabase()
    {
        var (store, _) = await Connect("admin");
        var (year, month) = (DemoSeeder.WorkDate.Year, DemoSeeder.WorkDate.Month);

        var report = await store.LoadAccountingAsync(year, month);
        var expected = _server.WithDb(db => AccountingReport.Load(db, year, month));

        Assert.NotEmpty(report.Shipment);
        Assert.Equal(Serialize(expected), Serialize(report));
    }

    [Fact]
    public async Task TariffChange_ThroughServer_ChangesUnpaidMonths()
    {
        var (store, _) = await Connect("admin");
        var unpaid = _server.WithDb(db => db.Payments.Count(p => p.SubscriberId == 1 && p.Year == Year && p.Month >= 9 && p.Amount == null));

        var months = await store.ChangeTariffAsync(new TariffChangeRequest(Year, 9, 777m, null, 1));

        Assert.True(unpaid > 0);
        Assert.Equal(unpaid, months);
        Assert.Equal(unpaid, _server.WithDb(db => db.Payments.Count(p => p.SubscriberId == 1 && p.Tariff == 777m)));
    }

    [Fact]
    public async Task ContractAndCourtCase_SavedThroughServer()
    {
        var (store, _) = await Connect("admin");
        var contract = new UpdateContractRequest("Д-77", new DateOnly(2026, 3, 1), null, "ООО «Ромашка»", "Проверка",
            LegalEntity: new LegalEntityRequest("5501234567", "550101001", null, "Омск, ул. Ленина, 1", "Иванов И.И."));
        var draft = new CourtCase { OrderNumber = "2-15/2026", OrderDate = new DateOnly(2026, 4, 10), Court = "Участок № 5", Amount = 1500m, StateDuty = 200m };

        await store.SaveContractAsync(1, contract);
        await store.SaveCourtCaseAsync(1, draft);

        var s = _server.WithDb(db => db.Subscribers.AsNoTracking().Single(x => x.Id == 1));
        Assert.Equal(("Д-77", true, "5501234567", "Омск, ул. Ленина, 1"), (s.ContractNumber, s.IsLegalEntity, s.Inn, s.LegalAddress));
        var saved = _server.WithDb(db => db.CourtCases.AsNoTracking().OrderBy(c => c.Id).Last(c => c.SubscriberId == 1));
        Assert.Equal(("2-15/2026", 1500m, 200m), (saved.OrderNumber, saved.Amount, saved.StateDuty));
    }

    [Fact]
    public async Task OekFile_ImportedThroughServer()
    {
        var (store, _) = await Connect("admin");
        var (account, tariff) = _server.WithDb(db => db.Payments.Where(p => p.Year == Year && p.Month == 8 && p.Amount == null)
            .Select(p => new { p.Subscriber.Account, p.Tariff }).AsEnumerable().Select(x => (x.Account, x.Tariff)).First());
        var file = new OekImportRequest(Year, 8,
        [
            new OekRow("83000000001", account, "", "", tariff, tariff, new DateOnly(Year, 8, 14)),
            new OekRow("83000000002", "нет-такого", "", "", 100m, 100m, new DateOnly(Year, 8, 14))
        ]);

        var result = await store.ImportOekAsync(file);

        Assert.Equal((2, 1, 1, tariff), (result.Rows, result.Matched, result.Payments, result.PaidTotal));
        Assert.Contains(result.Mismatches, m => m.Account == "нет-такого");
        Assert.Equal("ОЭК", _server.WithDb(db => db.Payments.Single(p => p.Subscriber.Account == account && p.Year == Year && p.Month == 8).Source));
    }

    [Fact]
    public async Task Reader_PaymentRefused_ShowsNoRights()
    {
        var (store, user) = await Connect("novikov");

        var error = await Assert.ThrowsAsync<StoreException>(() => store.RecordPaymentAsync(1, Year, null, user));

        Assert.Equal("Нет прав на это действие.", error.Message);
    }

    [Fact]
    public async Task ServerUnreachable_ShowsNoConnection()
    {
        var store = new ServerStore(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:9/") });

        var error = await Assert.ThrowsAsync<StoreException>(() => store.SignInAsync("admin", "avers"));

        Assert.StartsWith("Нет связи с сервером", error.Message);
    }

    private static int Year => DemoSeeder.WorkDate.Year;

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    private async Task<(ServerStore Store, AppUser User)> Connect(string login)
    {
        var store = new ServerStore(_server.CreateClient());
        return (store, await store.SignInAsync(login, "avers"));
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
            // На Windows файл ещё может держать запрос, который программа запустила в фоне после входа:
            // удаление временного файла не должно ронять тест, проверки которого уже прошли.
            for (var attempt = 0; File.Exists(_databaseFile); attempt++)
            {
                try
                {
                    File.Delete(_databaseFile);
                }
                catch (IOException) when (attempt < 10)
                {
                    await Task.Delay(200);
                }
                catch (IOException)
                {
                    break;
                }
            }
        }
    }
}
