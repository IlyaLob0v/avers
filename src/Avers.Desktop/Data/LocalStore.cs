using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Desktop.Data;

/// <summary>Учебная база SQLite на этом компьютере: у каждого сотрудника своя, рабочая дата — дата учебных данных.</summary>
public sealed class LocalStore(IDbContextFactory<AversDbContext> db) : IAversStore
{
    public bool IsShared => false;

    public string Title => "учебная база на этом компьютере";

    public DateOnly Today => DemoSeeder.WorkDate;

    /// <summary>Кто вошёл — для журнала изменений.</summary>
    private int _userId;

    public IReadOnlyList<AppUser> LoginUsers()
    {
        using var ctx = db.CreateDbContext();
        return ctx.Users.AsNoTracking().Where(x => !x.IsDisabled).OrderBy(x => x.Login).ToList();
    }

    public Task<AppUser> SignInAsync(string login, string password)
    {
        using var ctx = db.CreateDbContext();
        var user = ctx.Users.AsNoTracking().FirstOrDefault(u => u.Login == login && !u.IsDisabled)
                   ?? throw new StoreException("Выберите пользователя.");
        if (!Password.Verify(user, password))
            throw new StoreException("Неверный пароль. Для учебной базы пароль у всех: avers");
        _userId = user.Id;
        return Task.FromResult(user);
    }

    public void SignOut() { }

    public Task<AversSnapshot> LoadAsync(AppUser user)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(AversSnapshot.Load(ctx, user.Role).Link());
    }

    public Task<List<Subscriber>> LoadHouseAsync(House house)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(HouseSubscribers.Load(ctx, house.Id).Link(house));
    }

    public Task<(int Id, int HouseId)?> FindSubscriberAsync(SearchMode mode, string query, string street, string house, string apartment)
    {
        using var ctx = db.CreateDbContext();
        var (found, _, error) = SubscriberSearch.Where(ctx.Subscribers.AsNoTracking(), mode, query, street, house, apartment);
        if (found is null)
            throw new StoreException(error);
        var hit = found.OrderBy(s => s.LastName).ThenBy(s => s.Id).Select(s => new { s.Id, s.HouseId }).FirstOrDefault();
        return Task.FromResult(hit is null ? ((int, int)?)null : (hit.Id, hit.HouseId));
    }

    public Task<Debts> LoadDebtsAsync()
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(Debts.Load(ctx, Today));
    }

    public Task<AccountingReport> LoadAccountingAsync(int year, int month)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(AccountingReport.Load(ctx, year, month));
    }

    public Task<PaymentDto> RecordPaymentAsync(int subscriberId, int year, AppUser user)
    {
        using var ctx = db.CreateDbContext();
        var next = ctx.Payments.Where(p => p.SubscriberId == subscriberId && p.Year == year && p.Amount == null)
                       .OrderBy(p => p.Month).FirstOrDefault()
                   ?? throw new StoreException($"Все месяцы {year} уже оплачены.");
        var changes = ChangeLog.Diff(
            ("Сумма", next.Amount, next.Tariff),
            ("Дата оплаты", next.PaidAt, Today),
            ("Источник", next.Source, "Касса"));
        next.Amount = next.Tariff;
        next.Source = "Касса";
        next.PaidAt = Today;
        next.RecordedBy = user.DisplayName;
        var subscriber = ctx.Subscribers.First(s => s.Id == subscriberId);
        if (subscriber.LastPaymentDate is null || subscriber.LastPaymentDate < Today)
            subscriber.LastPaymentDate = Today;
        ChangeLog.Add(ctx, DateTimeOffset.UtcNow, ChangeLog.Who(ctx, user.Id), $"Оплата за {next.Month:00}.{next.Year}",
            ChangeLog.Account(subscriber), changes);
        ctx.SaveChanges();
        return Task.FromResult(new PaymentDto(next.Id, next.Year, next.Month, next.Tariff, next.Amount, next.PaidAt,
            next.Source, true, next.RecordedBy));
    }

    public Task SaveContractAsync(int subscriberId, UpdateContractRequest request)
    {
        using var ctx = db.CreateDbContext();
        var s = ctx.Subscribers.First(x => x.Id == subscriberId);
        var changes = ChangeLog.Diff(
            ("Номер договора", s.ContractNumber, request.ContractNumber),
            ("Заключён", s.ContractDate, request.ContractDate),
            ("Расторгнут", s.ContractTerminated, request.ContractTerminated),
            ("С кем заключён", s.ContractHolder, request.ContractHolder),
            ("Примечание", s.Notes, request.Notes));
        ChangeLog.Add(ctx, DateTimeOffset.UtcNow, ChangeLog.Who(ctx, _userId), "Договор и примечание", ChangeLog.Account(s), changes);
        s.ContractNumber = request.ContractNumber ?? "";
        s.ContractDate = request.ContractDate;
        s.ContractTerminated = request.ContractTerminated;
        s.ContractHolder = request.ContractHolder ?? "";
        s.Notes = request.Notes ?? "";
        ctx.SaveChanges();
        return Task.CompletedTask;
    }

    public Task SetPaymentWarningMonthsAsync(int months)
    {
        using var ctx = db.CreateDbContext();
        Settings.SetPaymentWarningMonths(ctx, months);
        return Task.CompletedTask;
    }

    public Task SaveHousePassportAsync(int houseId, HousePassportRequest request)
    {
        using var ctx = db.CreateDbContext();
        var h = ctx.Houses.Include(x => x.Street).First(x => x.Id == houseId);
        ChangeLog.Add(ctx, DateTimeOffset.UtcNow, ChangeLog.Who(ctx, _userId), "Паспорт дома", h.AddressLine, ChangeLog.Diff(
            ("Антенны, подъезды", h.Antennas, request.Antennas),
            ("Воздушные линии", h.AerialCables, request.AerialCables),
            ("Усилители", h.Amplifiers, request.Amplifiers),
            ("Блоки питания", h.PowerSupplies, request.PowerSupplies),
            ("Ключ техэтажа", h.TechFloorKey, request.TechFloorKey),
            ("Прочее", h.OtherNotes, request.OtherNotes)));
        h.Antennas = request.Antennas ?? "";
        h.AerialCables = request.AerialCables ?? "";
        h.Amplifiers = request.Amplifiers ?? "";
        h.PowerSupplies = request.PowerSupplies ?? "";
        h.TechFloorKey = request.TechFloorKey ?? "";
        h.OtherNotes = request.OtherNotes ?? "";
        ctx.SaveChanges();
        return Task.CompletedTask;
    }

    public Task<byte[]?> LoadSchemeFileAsync(int schemeId)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(SchemeFiles.Content(ctx, schemeId));
    }

    public Task AddSchemeAsync(int houseId, AddSchemeRequest request)
    {
        using var ctx = db.CreateDbContext();
        var house = ctx.Houses.Include(x => x.Street).First(x => x.Id == houseId);
        SchemeFiles.Add(ctx, house, request.Name, request.Content, Today, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task ReplaceSchemeFileAsync(int schemeId, byte[] content)
    {
        using var ctx = db.CreateDbContext();
        var scheme = ctx.HouseSchemes.Include(x => x.House).ThenInclude(h => h.Street).First(x => x.Id == schemeId);
        SchemeFiles.Replace(ctx, scheme, content, Today, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task RenameSchemeAsync(int schemeId, string name)
    {
        using var ctx = db.CreateDbContext();
        var scheme = ctx.HouseSchemes.Include(x => x.House).ThenInclude(h => h.Street).First(x => x.Id == schemeId);
        SchemeFiles.Rename(ctx, scheme, name, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task DeleteSchemeAsync(int schemeId)
    {
        using var ctx = db.CreateDbContext();
        var scheme = ctx.HouseSchemes.Include(x => x.House).ThenInclude(h => h.Street).First(x => x.Id == schemeId);
        SchemeFiles.Delete(ctx, scheme, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task SaveStreetAsync(Street draft) => EditPhonebook((ctx, who) => Phonebook.SaveStreet(ctx, draft, who, DateTimeOffset.UtcNow));

    public Task DeleteStreetAsync(int id) => EditPhonebook((ctx, who) => Phonebook.DeleteStreet(ctx, id, who, DateTimeOffset.UtcNow));

    public Task SaveHouseAsync(House draft) => EditPhonebook((ctx, who) => Phonebook.SaveHouse(ctx, draft, who, DateTimeOffset.UtcNow));

    public Task DeleteHouseAsync(int id) => EditPhonebook((ctx, who) => Phonebook.DeleteHouse(ctx, id, who, DateTimeOffset.UtcNow));

    public Task SaveMaterialAsync(Material draft) => EditPhonebook((ctx, who) => Phonebook.SaveMaterial(ctx, draft, who, DateTimeOffset.UtcNow));

    public Task DeleteMaterialAsync(int id) => EditPhonebook((ctx, who) => Phonebook.DeleteMaterial(ctx, id, who, DateTimeOffset.UtcNow));

    public Task SaveMechanicAsync(Mechanic draft) => EditPhonebook((ctx, who) => Phonebook.SaveMechanic(ctx, draft, who, DateTimeOffset.UtcNow));

    public Task DeleteMechanicAsync(int id) => EditPhonebook((ctx, who) => Phonebook.DeleteMechanic(ctx, id, who, DateTimeOffset.UtcNow));

    private Task EditPhonebook(Func<AversDbContext, string, string?> edit)
    {
        using var ctx = db.CreateDbContext();
        var error = edit(ctx, ChangeLog.Who(ctx, _userId));
        return error is null ? Task.CompletedTask : throw new StoreException(error);
    }

    public Task<int> AcceptTicketAsync(CreateTicketRequest request, AppUser user)
    {
        using var ctx = db.CreateDbContext();
        var ticket = new WorkTicket
        {
            Number = (ctx.Tickets.Max(t => (int?)t.Number) ?? 0) + 1,
            Date = Today,
            Time = TimeOnly.FromDateTime(DateTime.Now),
            HouseId = request.HouseId,
            SubscriberId = request.SubscriberId,
            Apartment = request.Apartment ?? "",
            Section = request.Section ?? "",
            Phone = request.Phone ?? "",
            Kind = Enum.TryParse<TicketKind>(request.Kind, out var kind) ? kind : TicketKind.Antenna,
            Fault = request.Fault,
            Status = TicketStatus.Accepted,
            AcceptedBy = user.DisplayName,
            AcceptedByUserId = user.Id
        };
        ctx.Tickets.Add(ticket);
        ctx.SaveChanges();
        return Task.FromResult(ticket.Number);
    }

    public Task SaveTicketAsync(int ticketId, string fault, string mechanic, string workDone)
    {
        using var ctx = db.CreateDbContext();
        var t = ctx.Tickets.First(x => x.Id == ticketId);
        t.Fault = fault;
        t.Mechanic = mechanic;
        TicketJournal.ApplyWork(t, workDone, Today);
        ctx.SaveChanges();
        return Task.CompletedTask;
    }

    public Task MarkPprAsync(int houseId, int year, int month, PprStatus status)
    {
        using var ctx = db.CreateDbContext();
        PprPlan.Mark(ctx, houseId, year, month, status, Today);
        return Task.CompletedTask;
    }

    public Task<int> CopyPprAsync(int year)
    {
        using var ctx = db.CreateDbContext();
        var copied = PprPlan.CopyToNextYear(ctx, year);
        return copied > 0
            ? Task.FromResult(copied)
            : throw new StoreException($"План на {year + 1} год уже есть или план {year} года пуст.");
    }

    public Task<IReadOnlyList<AppUser>> LoadUsersAsync()
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult<IReadOnlyList<AppUser>>(UserAccounts.List(ctx));
    }

    public Task SaveUserAsync(AppUser draft, string password, AppUser admin)
    {
        using var ctx = db.CreateDbContext();
        var error = UserAccounts.Save(ctx, draft, password, admin.Id, DateTimeOffset.UtcNow);
        return error is null ? Task.CompletedTask : throw new StoreException(error);
    }

    public Task<IReadOnlyList<ChangeLogEntry>> LoadChangeLogAsync()
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult<IReadOnlyList<ChangeLogEntry>>(ChangeLog.Latest(ctx));
    }
}
