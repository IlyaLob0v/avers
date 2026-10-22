using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Desktop.Data;

/// <summary>Учебная база SQLite на этом компьютере: у каждого сотрудника своя. Рабочая дата — сегодня, в тестах — дата учебных данных.</summary>
public sealed class LocalStore(IDbContextFactory<AversDbContext> db, DateOnly today) : IAversStore
{
    public bool IsShared => false;

    public string Title => "учебная база на этом компьютере";

    public DateOnly Today => today;

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

    public Task<PaymentDto> RecordPaymentAsync(int subscriberId, int year, decimal? amount, AppUser user)
    {
        using var ctx = db.CreateDbContext();
        var next = ctx.Payments.Where(p => p.SubscriberId == subscriberId && p.Year == year && p.Amount == null)
                       .OrderBy(p => p.Month).FirstOrDefault()
                   ?? throw new StoreException($"Все месяцы {year} уже оплачены.");
        var paid = amount ?? next.Tariff;
        var changes = ChangeLog.Diff(
            ("Сумма", next.Amount, paid),
            ("Дата оплаты", next.PaidAt, Today),
            ("Источник", next.Source, "Касса"));
        next.Amount = paid;
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

    public Task SaveCourtCaseAsync(int subscriberId, CourtCase draft) =>
        EditPhonebook((ctx, who) => CourtCases.Save(ctx, subscriberId, draft, who, DateTimeOffset.UtcNow));

    public Task CancelPaymentAsync(int subscriberId, int paymentId) =>
        EditPhonebook((ctx, who) => PaymentCancellation.Cancel(ctx, subscriberId, paymentId, who, DateTimeOffset.UtcNow));

    public Task SaveContractAsync(int subscriberId, UpdateContractRequest request)
    {
        using var ctx = db.CreateDbContext();
        var s = ctx.Subscribers.First(x => x.Id == subscriberId);
        var draft = new Subscriber
        {
            ContractNumber = request.ContractNumber ?? "",
            ContractDate = request.ContractDate,
            ContractTerminated = request.ContractTerminated,
            ContractHolder = request.ContractHolder ?? "",
            Notes = request.Notes ?? ""
        };
        SubscriberContracts.Save(ctx, s, draft, Today, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task AddApartmentAsync(Subscriber draft, decimal tariff) =>
        EditPhonebook((ctx, who) => Apartments.Add(ctx, draft, tariff, Today, who, DateTimeOffset.UtcNow));

    public Task SetPaymentWarningMonthsAsync(int months)
    {
        using var ctx = db.CreateDbContext();
        Settings.SetPaymentWarningMonths(ctx, months, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task SetPaymentConfirmFromAsync(decimal amount)
    {
        using var ctx = db.CreateDbContext();
        Settings.SetPaymentConfirmFrom(ctx, amount, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<int> ChangeTariffAsync(TariffChangeRequest r)
    {
        using var ctx = db.CreateDbContext();
        var (error, months) = Tariffs.Change(ctx, r.Year, r.Month, r.Tariff, r.HouseId, r.SubscriberId,
            ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return error is null ? Task.FromResult(months) : throw new StoreException(error);
    }

    public Task<int> OpenYearAsync(int year)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(Accruals.OpenYear(ctx, year, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow));
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

    public Task<List<DocumentInfo>> LoadDocumentsAsync(int subscriberId)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(SubscriberDocuments.List(ctx, subscriberId));
    }

    public Task<byte[]?> LoadDocumentFileAsync(int documentId)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(SubscriberDocuments.File(ctx, documentId)?.Content);
    }

    public Task AddDocumentAsync(int subscriberId, AddDocumentRequest request)
    {
        using var ctx = db.CreateDbContext();
        var subscriber = ctx.Subscribers.First(x => x.Id == subscriberId);
        SubscriberDocuments.Add(ctx, subscriber, request.Kind, request.Name, request.Content, Today, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task DeleteDocumentAsync(int documentId)
    {
        using var ctx = db.CreateDbContext();
        SubscriberDocuments.Delete(ctx, documentId, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task SaveIntercomPanelAsync(int houseId, IntercomPanel draft)
    {
        using var ctx = db.CreateDbContext();
        return Check(Intercoms.SavePanel(ctx, houseId, draft, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow));
    }

    public Task AddIntercomTicketAsync(int houseId, IntercomTicket draft)
    {
        using var ctx = db.CreateDbContext();
        return Check(Intercoms.AddTicket(ctx, houseId, draft, Today, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow));
    }

    public Task CloseIntercomTicketAsync(int ticketId)
    {
        using var ctx = db.CreateDbContext();
        return Check(Intercoms.MarkDone(ctx, ticketId, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow));
    }

    private static Task Check(string? error) => error is null ? Task.CompletedTask : throw new StoreException(error);

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
        var before = ChangeLog.TicketState(t);
        t.Fault = fault;
        t.Mechanic = mechanic;
        TicketJournal.ApplyWork(t, workDone, Today);
        ChangeLog.TicketEdit(ctx, DateTimeOffset.UtcNow, ChangeLog.Who(ctx, _userId), t, before);
        ctx.SaveChanges();
        return Task.CompletedTask;
    }

    public Task MarkPprAsync(int houseId, int year, int month, PprStatus status)
    {
        using var ctx = db.CreateDbContext();
        PprPlan.Mark(ctx, houseId, year, month, status, Today, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<int> CopyPprAsync(int year)
    {
        using var ctx = db.CreateDbContext();
        var copied = PprPlan.CopyToNextYear(ctx, year, ChangeLog.Who(ctx, _userId), DateTimeOffset.UtcNow);
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
