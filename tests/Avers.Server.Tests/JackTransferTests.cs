using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Avers.Server.Tests;

/// <summary>
/// Перенос JACK → Аверс (db/migration/transfer) на учебной копии JACK: база Аверса создаётся миграциями,
/// затем выполняются те же скрипты, что на стенде и в день переезда. Только на PostgreSQL (AVERS_TEST_POSTGRES),
/// без него тесты ничего не проверяют.
/// </summary>
public sealed class JackTransferTests : IAsyncLifetime
{
    private static readonly string? Postgres = Environment.GetEnvironmentVariable("AVERS_TEST_POSTGRES");
    private static readonly string Scripts = Path.Combine(RepoRoot(), "db", "migration", "transfer");
    private readonly string _database = $"avers_transfer_{Guid.NewGuid():N}";

    private string ConnectionString => $"{Postgres};Database={_database};Pooling=False";

    public async Task InitializeAsync()
    {
        if (Postgres is null)
            return;
        await using var db = Context();
        await db.Database.MigrateAsync();
        await RunAsync("test/legacy-sample.sql");
        await RunAsync("transfer.sql");
    }

    public async Task DisposeAsync()
    {
        if (Postgres is null)
            return;
        await using var connection = new NpgsqlConnection($"{Postgres};Database=postgres;Pooling=False");
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Check_AllMatches()
    {
        if (Postgres is null)
            return;

        await RunAsync("check.sql");

        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT title, jack, avers, status FROM recon.transfer_check ORDER BY n", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new Dictionary<string, (decimal? Jack, decimal? Avers, string Status)>();
        while (await reader.ReadAsync())
            rows[reader.GetString(0)] = (reader.IsDBNull(1) ? null : reader.GetDecimal(1), reader.IsDBNull(2) ? null : reader.GetDecimal(2), reader.GetString(3));
        Assert.DoesNotContain(rows.Values, r => r.Status == "РАСХОЖДЕНИЕ");
        Assert.Equal((3m, 3m), (rows["Абоненты (без пустых)"].Jack!.Value, rows["Абоненты (без пустых)"].Avers!.Value));
        Assert.Equal(1705m, rows["Долг всех абонентов, сумма"].Avers);
        Assert.Equal(1m, rows["Пустые абоненты для заявок на дом (не переносятся)"].Jack);
        Assert.Equal(1m, rows["Проверки подключения без заявки (не переносятся)"].Jack);
        Assert.Equal(1m, rows["Оплаты до 1998 года, неденоминированные (не переносятся), штук"].Jack);
    }

    [Fact]
    public async Task Check_FailsWhenPaymentDiffers()
    {
        if (Postgres is null)
            return;
        await ExecuteAsync("""UPDATE "Payments" SET "Amount" = "Amount" + 0.01 WHERE "SubscriberId" = 1001 AND "Year" = 2026 AND "Month" = 1""");

        var error = await Assert.ThrowsAsync<PostgresException>(() => RunAsync("check.sql"));

        Assert.Contains("есть расхождения", error.MessageText);
    }

    [Fact]
    public async Task Transfer_RefusesNonEmptyDatabase()
    {
        if (Postgres is null)
            return;

        var error = await Assert.ThrowsAsync<PostgresException>(() => RunAsync("transfer.sql"));

        Assert.Contains("только в пустую базу", error.MessageText);
    }

    [Fact]
    public async Task Subscribers_KeepJackFields()
    {
        if (Postgres is null)
            return;
        await using var db = Context();

        var ivanova = await db.Subscribers.SingleAsync(s => s.Id == 1001);
        var petrov = await db.Subscribers.SingleAsync(s => s.Id == 1002);

        Assert.Equal(("Иванова", "1001", "12", "", "А-17"), (ivanova.LastName, ivanova.Account, ivanova.Apartment, ivanova.Section, ivanova.InnerCode));
        Assert.Equal(("9001", new DateOnly(2010, 2, 1), (DateOnly?)null), (ivanova.ContractNumber, ivanova.ContractDate.GetValueOrDefault(), ivanova.ContractTerminated));
        Assert.Equal(("12", "2222"), (ivanova.PzhreuIndex, ivanova.PzhreuAccount));
        Assert.Equal("коллективная оплата", ivanova.Benefit);
        Assert.Equal(Subscriber.NoBenefit, petrov.Benefit);
        Assert.Equal(new DateOnly(2026, 3, 10), ivanova.LastPaymentDate);
        Assert.Equal(2026, ivanova.SettlementFrom);
        Assert.False(await db.Subscribers.AnyAsync(s => s.Id == 1004));
        Assert.Equal(["Ленина пр.", "Ленина ул.", "Учебная"], await db.Streets.OrderBy(s => s.Name).Select(s => s.Name).ToListAsync());
    }

    [Fact]
    public async Task Payments_OneRowPerMonth()
    {
        if (Postgres is null)
            return;
        await using var db = Context();

        var ivanova = await db.Payments.Where(p => p.SubscriberId == 1001).OrderBy(p => p.Month).ToListAsync();
        var petrovJanuary = await db.Payments.SingleAsync(p => p.SubscriberId == 1002 && p.Year == 2026 && p.Month == 1);
        var receipt = await db.UnifiedReceipts.SingleAsync();

        Assert.Equal(12, ivanova.Count);
        Assert.Equal((150m, 150m, "KASSIR2", "Почта", new DateOnly(2026, 1, 20)),
            (ivanova[0].Tariff, ivanova[0].Amount.GetValueOrDefault(), ivanova[0].RecordedBy, ivanova[0].Source, ivanova[0].PaidAt.GetValueOrDefault()));
        Assert.Equal("ПЖРЭУ", ivanova[1].Source);
        Assert.Equal(0m, ivanova[2].Amount);
        Assert.Null(ivanova[3].Amount);
        Assert.Equal(105m, petrovJanuary.Tariff);
        Assert.Equal((1001, 150m, 150m, "А-17"), (receipt.SubscriberId, receipt.Paid, receipt.Amount, receipt.InnerCode));
    }

    [Fact]
    public async Task Debts_SameAsJack()
    {
        if (Postgres is null)
            return;
        await using var db = Context();

        var debts = Debts.Load(db, new DateOnly(2027, 1, 15));

        Assert.Equal(1500m, debts.Debtors.Single(d => d.SubscriberId == 1001).Debt);
        Assert.Equal(1705m, debts.ByHouse.Values.Sum());
    }

    [Fact]
    public async Task Tickets_StatusKindAndMechanic()
    {
        if (Postgres is null)
            return;
        await using var db = Context();

        var tickets = await db.Tickets.OrderBy(t => t.Id).ToListAsync();

        Assert.Equal([70001, 70002, 70003, 70004], tickets.Select(t => t.Number));
        Assert.Equal((TicketKind.Antenna, TicketStatus.Accepted, new TimeOnly(9, 15), "Механиков Пётр Ильич"),
            (tickets[0].Kind, tickets[0].Status, tickets[0].Time, tickets[0].Mechanic));
        Assert.Equal((TicketKind.Cable, TicketStatus.Done, new DateOnly(2026, 8, 5), "Заменили кабель. Отчёт ПЖРЭУ"),
            (tickets[1].Kind, tickets[1].Status, tickets[1].DoneDate.GetValueOrDefault(), tickets[1].WorkDone));
        Assert.Equal((TicketStatus.Done, "Заявка отменена"), (tickets[2].Status, tickets[2].WorkDone));
        Assert.Equal(((int?)null, 101, ""), (tickets[3].SubscriberId, tickets[3].HouseId, tickets[3].Apartment));
    }

    [Fact]
    public async Task NewRecords_GetKeysAfterJack()
    {
        if (Postgres is null)
            return;
        await using var db = Context();

        db.Streets.Add(new Street { Name = "Новая" });
        await db.SaveChangesAsync();

        Assert.Equal(13, await db.Streets.Where(s => s.Name == "Новая").Select(s => s.Id).SingleAsync());
        Assert.Equal([PprStatus.Done, PprStatus.Overdue, PprStatus.Planned], await db.PprEntries.OrderBy(p => p.Id).Select(p => p.Status).ToListAsync());
        Assert.Equal("Участок № 1 (учебный)", await db.Houses.Where(h => h.Id == 100).Select(h => h.Zone).SingleAsync());
        Assert.Equal(1, await db.Mechanics.CountAsync());
    }

    private AversDbContext Context() => new(new DbContextOptionsBuilder<AversDbContext>().UseNpgsql(ConnectionString).Options);

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task RunAsync(string script) => await ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(Scripts, script)));

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Avers.sln")))
            dir = dir.Parent;
        return dir.FullName;
    }
}
