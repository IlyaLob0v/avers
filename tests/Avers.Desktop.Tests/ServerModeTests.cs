using System.Text.Json;
using Avalonia.Headless.XUnit;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Payment_WhenServerReplyOrRefreshFails_RecordsOnlyTheChosenMonth(bool loseReply)
    {
        using var client = new HttpClient(new PaymentConnectionFault(_server.Server.CreateHandler(), loseReply))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var vm = await SignIn("orlov", client);
        var id = vm.SelectedSubscriber!.Id;
        var before = _server.WithDb(db => db.Payments.Count(p => p.SubscriberId == id && p.Amount != null));

        await vm.RecordPaymentCommand.ExecuteAsync(null);
        if (loseReply)
        {
            Assert.Contains("повторит ту же операцию", vm.PaymentStatus);
            await vm.RecordPaymentCommand.ExecuteAsync(null);
        }
        else
            Assert.Contains("Карточку обновить не удалось", vm.PaymentStatus);

        Assert.StartsWith("Внесена оплата", vm.PaymentStatus);
        Assert.Equal(before + 1, _server.WithDb(db => db.Payments.Count(p => p.SubscriberId == id && p.Amount != null)));
        Assert.Equal(1, _server.WithDb(db => db.PaymentOperations.Count()));
    }

    [AvaloniaFact]
    public async Task Map_OpensServerPageOnSelectedHouse()
    {
        var vm = await SignIn("sokolova");
        var opened = new List<string>();
        vm.OpenFile = opened.Add;

        vm.OpenMapCommand.Execute(null);

        Assert.True(vm.CanOpenMap);
        var address = new Uri(Assert.Single(opened));
        Assert.Equal($"#map={vm.SelectedHouse!.Id}", address.Fragment);
        Assert.Equal($"?v={typeof(ServerStore).Assembly.GetName().Version?.ToString(3)}", address.Query);
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

    [AvaloniaTheory]
    [InlineData(SearchMode.Surname, " ", "Введите фамилию.")]
    [InlineData(SearchMode.Account, "NO-SUCH-ACCOUNT", "Абонент не найден.")]
    public async Task Find_UnsuccessfulSearch_ClearsSubscriberAndPreventsPayment(SearchMode mode, string query, string message)
    {
        var vm = await SignIn("orlov");
        var subscriber = vm.SelectedSubscriber!;
        var before = _server.WithDb(db => db.Payments.Count(p => p.SubscriberId == subscriber.Id && p.Amount != null));

        vm.SearchMode = mode;
        vm.FindQuery = query;
        await vm.FindCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.Status);
        Assert.Null(vm.SelectedSubscriber);
        await vm.RecordPaymentCommand.ExecuteAsync(null);
        Assert.Equal(before, _server.WithDb(db => db.Payments.Count(p => p.SubscriberId == subscriber.Id && p.Amount != null)));

        vm.SearchMode = SearchMode.Account;
        vm.FindQuery = subscriber.Account;
        await vm.FindCommand.ExecuteAsync(null);

        Assert.Equal(subscriber.Id, vm.SelectedSubscriber!.Id);
    }

    [Fact]
    public async Task Find_SeveralApartments_WaitsForChoice()
    {
        var vm = await SignIn("orlov");
        vm.SearchMode = SearchMode.Address;
        vm.FindStreet = DemoSeeder.StartStreet;
        vm.FindHouse = DemoSeeder.StartHouse;
        vm.FindApartment = "";
        vm.FindSection = "";

        await vm.FindCommand.ExecuteAsync(null);
        Assert.True(vm.SearchMatches.Count > 1);
        Assert.Null(vm.SelectedSubscriber);
        var chosen = vm.SearchMatches.Last();

        await vm.OpenSearchMatchCommand.ExecuteAsync(chosen);

        Assert.Equal(chosen.Id, vm.SelectedSubscriber!.Id);
        Assert.Empty(vm.SearchMatches);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Find_AccountUsesExactIdentifierOrAsksForPartialMatches(bool exact)
    {
        var vm = await SignIn("orlov");
        var account = vm.SelectedSubscriber!.Account;
        var houseId = vm.SelectedHouse!.Id;
        _server.WithDb(db =>
        {
            db.Subscribers.Add(new Subscriber { HouseId = houseId, Account = account + "0", LastName = "Проверка", Apartment = "999" });
            return db.SaveChanges();
        });
        vm.SearchMode = SearchMode.Account;
        vm.FindQuery = exact ? account : account[..^1];

        await vm.FindCommand.ExecuteAsync(null);

        if (exact)
        {
            Assert.Equal(account, vm.SelectedSubscriber?.Account);
            Assert.Empty(vm.SearchMatches);
            return;
        }
        Assert.Null(vm.SelectedSubscriber);
        Assert.True(vm.SearchMatches.Count > 1);
        Assert.Contains(vm.SearchMatches, match => match.Account == account);
        Assert.Contains(vm.SearchMatches, match => match.Account == account + "0");
    }

    [AvaloniaTheory]
    [InlineData(true, AppModule.Desktop)]
    [InlineData(false, AppModule.Abon)]
    [InlineData(false, AppModule.Contract)]
    public async Task Find_LateReply_DoesNotRestoreDiscardedSearch(bool logout, AppModule destination)
    {
        using var delay = new ResponseDelayHandler(_server.Server.CreateHandler(), "/api/subscribers");
        using var client = new HttpClient(delay) { BaseAddress = new Uri("http://localhost/") };
        var vm = await SignIn("orlov", client);
        vm.SearchMode = SearchMode.Address;
        vm.FindStreet = DemoSeeder.StartStreet;
        vm.FindHouse = DemoSeeder.StartHouse;
        vm.FindSection = vm.FindApartment = "";
        delay.PauseNext();
        var search = vm.FindCommand.ExecuteAsync(null);
        try
        {
            await delay.ResponseReady.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            if (logout) vm.LogoutCommand.Execute(null);
            else vm.OpenModuleCommand.Execute(destination);
        }
        finally { delay.Release(); }
        await search;

        Assert.Null(vm.SelectedSubscriber);
        Assert.Empty(vm.SearchMatches);
        if (logout) Assert.Null(vm.User);
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

        var paid = await store.RecordPaymentAsync(subscriberId, new RecordPaymentRequest(Year), user);
        var saved = _server.WithDb(db => db.Payments.AsNoTracking().Single(p => p.Id == paid.Id));
        Assert.True(paid.IsPaid);
        Assert.Equal((paid.Month, paid.Tariff, "Касса"), (saved.Month, saved.Amount!.Value, saved.Source!));

        await store.CancelPaymentAsync(subscriberId, paid.Id, paid.Revision);

        Assert.Null(_server.WithDb(db => db.Payments.AsNoTracking().Single(p => p.Id == paid.Id).Amount));
    }

    [Fact]
    public async Task CancelUnpaidMonth_ShowsServerReason()
    {
        var (store, user) = await Connect("admin");
        var paid = await store.RecordPaymentAsync(1, new RecordPaymentRequest(Year), user);
        await store.CancelPaymentAsync(1, paid.Id, paid.Revision);

        var error = await Assert.ThrowsAsync<StoreException>(() => store.CancelPaymentAsync(1, paid.Id, paid.Revision + 1));

        Assert.Equal($"Месяц {paid.Month:00}.{Year} не оплачен.", error.Message);
    }

    [Fact]
    public async Task Debts_SameAsInDatabase()
    {
        var (store, _) = await Connect("admin");

        var debts = await store.LoadDebtsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var expected = _server.WithDb(db => Debts.Load(db, store.Today));

        Assert.NotEmpty(debts.Debtors);
        Assert.Equal(Serialize(expected.Debtors), Serialize(debts.Debtors));
        Assert.Equal(_server.WithDb(db => Debts.LoadHouseTotals(db, store.Today)), await store.LoadDebtTotalsAsync());
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
    public async Task Debtors_ExportIncludesAllFilteredPages()
    {
        var vm = await SignIn("lawyer");
        _server.WithDb(db =>
        {
            var house = db.Houses.First();
            for (var i = 1; i <= 125; i++)
                db.Subscribers.Add(new Subscriber { HouseId = house.Id, Account = $"EXPORT-{i}", LastName = "Проверка выгрузки" });
            db.SaveChanges();
            return 0;
        });
        vm.GoCommand.Execute(AppModule.Legal);
        var screen = vm.LegalScreen;
        await screen.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(Debts.PageSize, screen.Debtors.Count);
        await screen.NextPageCommand.ExecuteAsync(null);
        Assert.Contains("101–", screen.DebtorsCountText);
        string? opened = null;
        vm.OpenFile = path => opened = path;
        await screen.ExportDebtorsCommand.ExecuteAsync(null);
        using var book = new ClosedXML.Excel.XLWorkbook(opened!);
        var accounts = book.Worksheet(1).Column(3).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.All(Enumerable.Range(1, 125), i => Assert.Contains($"EXPORT-{i}", accounts));
    }

    [Fact]
    public async Task OriginalArchive_IsReadThroughServerAndClearedOnLogout()
    {
        var vm = await SignIn("admin");
        var id = vm.SelectedSubscriber!.Id;
        _server.WithDb(db =>
        {
            db.JackPayments.Add(new JackPayment
            {
                Id = 3_000_000_000,
                SubscriberId = id,
                Period = new DateTime(1996, 1, 1),
                PaidAt = new DateTime(1996, 2, 3),
                Amount = 15000,
                Source = "Касса"
            });
            db.SaveChanges();
            return 0;
        });
        vm.ArchiveScreen.IsExpanded = true;
        await vm.ArchiveScreen.ReloadCommand.ExecuteAsync(null);
        Assert.Equal(15000, Assert.Single(vm.ArchiveScreen.Rows).Amount);
        vm.LogoutCommand.Execute(null);
        Assert.Empty(vm.ArchiveScreen.Rows);
        Assert.False(vm.ArchiveScreen.IsExpanded);
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

        var error = await Assert.ThrowsAsync<StoreException>(() => store.RecordPaymentAsync(1, new RecordPaymentRequest(Year), user));

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

    private async Task<MainViewModel> SignIn(string login, HttpClient? client = null)
    {
        var vm = App.CreateMainViewModel(new ServerStore(client ?? _server.CreateClient()));
        vm.LoginName = login;
        vm.Password = "avers";
        await vm.LoginCommand.ExecuteAsync(null);
        Assert.Null(vm.LoginError);
        if (vm.User is { } user && RoleCatalog.Can(user.Role, AppModule.Abon, out _))
        {
            vm.SearchMode = SearchMode.Account;
            vm.FindQuery = DemoSeeder.StartAccount;
            await vm.FindCommand.ExecuteAsync(null);
        }
        else
        {
            vm.SelectedStreet = vm.Streets.FirstOrDefault(s => s.Name == DemoSeeder.StartStreet);
            vm.SelectedHouse = vm.HousesOnStreet.FirstOrDefault(h => h.Number == DemoSeeder.StartHouse);
        }
        vm.TicketsScreen.SelectedTicket = vm.TicketsScreen.Tickets.FirstOrDefault(ticket => ticket.Number == DemoSeeder.StartTicket);
        return vm;
    }

    public async ValueTask DisposeAsync() => await _server.DisposeAsync();

    private sealed class PaymentConnectionFault(HttpMessageHandler inner, bool loseReply) : DelegatingHandler(inner)
    {
        private bool _recorded;
        private bool _failed;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var path = request.RequestUri!.AbsolutePath;
            var recording = request.Method == HttpMethod.Post && path.EndsWith("/payments", StringComparison.Ordinal);
            _recorded |= recording && response.IsSuccessStatusCode;
            if (_recorded && !_failed && (loseReply ? recording : path == "/api/desktop/snapshot"))
            {
                _failed = true;
                response.Dispose();
                throw new HttpRequestException("Проверка обрыва соединения.");
            }
            return response;
        }
    }

    internal sealed class ResponseDelayHandler(HttpMessageHandler inner, string path) : DelegatingHandler(inner)
    {
        private int _armed;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResponseReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void PauseNext() => Interlocked.Exchange(ref _armed, 1);
        public void Release() => _release.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.RequestUri!.AbsolutePath == path && Interlocked.Exchange(ref _armed, 0) == 1)
            {
                ResponseReady.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }
            return response;
        }
    }

    internal sealed class SharedServer : WebApplicationFactory<Program>
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
