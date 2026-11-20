using System.Diagnostics;
using System.Text.Json;
using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Только искусственные записи в новой отдельной базе. Существующие базы не изменяются.
var accounts = args.Length > 0 ? int.Parse(args[0]) : 10000;
var months = args.Length > 1 ? int.Parse(args[1]) : 348;
var concurrency = args.Length > 2 ? int.Parse(args[2]) : 16;
if (accounts is < 2 or > 200000 || months is < 12 or > 348 || concurrency is < 1 or > 32)
    throw new ArgumentException("Счета: 2–200000; месяцы: 12–348; одновременные запросы: 1–32.");
var connection = Environment.GetEnvironmentVariable("AVERS_TEST_POSTGRES")
    ?? throw new InvalidOperationException("Задайте AVERS_TEST_POSTGRES для изолированного PostgreSQL стенда.");
var database = $"avers_probe_{Guid.NewGuid():N}";
var target = new NpgsqlConnectionStringBuilder(connection) { Database = database, CommandTimeout = 600 };
var adminConnection = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false };
var options = new DbContextOptionsBuilder<AversDbContext>().UseNpgsql(target.ConnectionString).Options;
var today = new DateOnly(2027, 1, 15);
await using var admin = new NpgsqlConnection(adminConnection.ConnectionString);
await admin.OpenAsync();
try
{
    Console.Error.WriteLine($"Готовлю отдельную базу: {accounts} счетов × {months} месяцев.");
    await using (var db = new AversDbContext(options))
    {
        await db.Database.MigrateAsync();
        var street = new Street { Name = "Нагрузочная" };
        var houses = Enumerable.Range(1, Math.Max(2, accounts / 75))
            .Select(i => new House { Street = street, Number = i.ToString() }).ToList();
        var largeHouse = Math.Min(1000, accounts / 2);
        for (var i = 1; i <= accounts; i++)
            db.Subscribers.Add(new Subscriber
            {
                House = houses[i <= largeHouse ? 0 : 1 + i % (houses.Count - 1)],
                Account = $"TEST-{i}",
                LastName = $"Абонент {i}",
                Apartment = i.ToString(),
                LastPaymentDate = new DateOnly(2026, 11, 15),
                IsLegalEntity = i % 20 == 0
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Payments" ("SubscriberId", "Year", "Month", "Tariff", "Amount", "PaidAt", "ImportedFromJack")
            SELECT s."Id", 1998 + m.n / 12, m.n % 12 + 1, 150,
                   CASE WHEN m.n % 3 <> 0 THEN 150 END,
                   CASE WHEN m.n % 3 <> 0 THEN make_date(1998 + m.n / 12, m.n % 12 + 1, 15) END,
                   m.n % 3 <> 0
              FROM "Subscribers" s CROSS JOIN generate_series(0, {months - 1}) m(n)
            """);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO jack_archive.payment_history
                (payid, subscriber_id, period, paid_at, amount, pay_type_id, connection_type_id, source)
            SELECT "Id", "SubscriberId", make_date("Year", "Month", 1), "PaidAt", "Amount", 1, 1, 'Тестовый источник'
              FROM "Payments" WHERE "Amount" IS NOT NULL
            """);
        await db.Database.ExecuteSqlRawAsync("ANALYZE");
    }

    var measurements = new List<object>();
    object Measure(string name, Func<AversDbContext, object> read)
    {
        using (var warm = new AversDbContext(options)) read(warm);
        var elapsed = new List<double>();
        var payload = 0;
        for (var i = 0; i < 5; i++)
        {
            using var db = new AversDbContext(options);
            var timer = Stopwatch.StartNew();
            payload = JsonSerializer.SerializeToUtf8Bytes(read(db)).Length;
            elapsed.Add(timer.Elapsed.TotalMilliseconds);
        }
        elapsed.Sort();
        return new { Query = name, MedianMs = Math.Round(elapsed[2], 1), MaxMs = Math.Round(elapsed[^1], 1), PayloadBytes = payload };
    }
    Console.Error.WriteLine("Проверяю целостность сценария и измеряю запросы.");
    using (var db = new AversDbContext(options))
    {
        var house = HouseSubscribers.Load(db, 1);
        if (house.Payments.Count != months || house.Subscribers.Count != Math.Min(1000, accounts / 2))
            throw new InvalidOperationException("История дома содержит лишние или потерянные месяцы.");
        var page = Debts.Load(db, today);
        if (page.TotalCount != accounts || page.Debtors.Count != Math.Min(accounts, Debts.PageSize))
            throw new InvalidOperationException("Страница должников или общее количество не совпали с эталоном.");
        var expectedDebt = 150m * ((months + 2) / 3) * accounts;
        if (Debts.LoadHouseTotals(db, today).Values.Sum() != expectedDebt)
            throw new InvalidOperationException("Денежный итог не совпал с искусственным эталоном.");
        var archive = SubscriberArchive.Load(db, house.Subscribers[0].Id);
        if (archive.TotalCount != months - (months + 2) / 3)
            throw new InvalidOperationException("Архив содержит неверное число исходных операций.");
    }
    measurements.Add(Measure("Дом: список квартир и история выбранного счёта", db => HouseSubscribers.Load(db, 1)));
    measurements.Add(Measure("Должники: первая страница", db => Debts.Load(db, today)));
    measurements.Add(Measure("Должники: отбор по адресу и типу клиента", db => Debts.Load(db, today, new(Address: "Нагрузочная 1", IsLegalEntity: true))));
    measurements.Add(Measure("Долги по домам", db => Debts.LoadHouseTotals(db, today)));
    measurements.Add(Measure("Итоги для 1С", db => AccountingReport.Load(db, 1998 + (months - 1) / 12, (months - 1) % 12 + 1)));

    var simultaneous = await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => Task.Run(() =>
    {
        using var db = new AversDbContext(options);
        var timer = Stopwatch.StartNew();
        var page = Debts.Load(db, today);
        if (page.TotalCount != accounts) throw new InvalidOperationException("Параллельный запрос потерял должников.");
        return timer.Elapsed.TotalMilliseconds;
    })));
    using var final = new AversDbContext(options);
    await using var size = new NpgsqlCommand("SELECT pg_database_size(current_database())", (NpgsqlConnection)final.Database.GetDbConnection());
    await final.Database.OpenConnectionAsync();
    var report = new
    {
        Kind = "Синтетическая проверка запросов; без HTTP, интерфейса и нагрузки 1С",
        Accounts = accounts,
        Months = months,
        MonthlyRows = (long)accounts * months,
        OriginalOperations = (long)accounts * (months - (months + 2) / 3),
        DatabaseBytes = (long)(await size.ExecuteScalarAsync())!,
        Measurements = measurements,
        Simultaneous = new { Requests = concurrency, MedianMs = Math.Round(simultaneous.Order().ElementAt(concurrency / 2), 1), MaxMs = Math.Round(simultaneous.Max(), 1) }
    };
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}
finally
{
    NpgsqlConnection.ClearAllPools();
    await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
    await drop.ExecuteNonQueryAsync();
}
