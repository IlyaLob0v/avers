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

    public IReadOnlyList<AppUser> LoginUsers()
    {
        using var ctx = db.CreateDbContext();
        return ctx.Users.AsNoTracking().OrderBy(x => x.Login).ToList();
    }

    public Task<AppUser> SignInAsync(string login, string password)
    {
        using var ctx = db.CreateDbContext();
        var user = ctx.Users.AsNoTracking().FirstOrDefault(u => u.Login == login)
                   ?? throw new StoreException("Выберите пользователя.");
        if (!Password.Verify(user, password))
            throw new StoreException("Неверный пароль. Для учебной базы пароль у всех: avers");
        return Task.FromResult(user);
    }

    public void SignOut() { }

    public Task<AversSnapshot> LoadAsync(AppUser user)
    {
        using var ctx = db.CreateDbContext();
        return Task.FromResult(AversSnapshot.Load(ctx, user.Role).Link());
    }

    public Task<PaymentDto> RecordPaymentAsync(int subscriberId, int year, AppUser user)
    {
        using var ctx = db.CreateDbContext();
        var next = ctx.Payments.Where(p => p.SubscriberId == subscriberId && p.Year == year && p.Amount == null)
                       .OrderBy(p => p.Month).FirstOrDefault()
                   ?? throw new StoreException($"Все месяцы {year} уже оплачены.");
        next.Amount = next.Tariff;
        next.Source = "Касса";
        next.PaidAt = Today;
        next.RecordedBy = user.DisplayName;
        var subscriber = ctx.Subscribers.First(s => s.Id == subscriberId);
        if (subscriber.LastPaymentDate is null || subscriber.LastPaymentDate < Today)
            subscriber.LastPaymentDate = Today;
        ctx.SaveChanges();
        return Task.FromResult(new PaymentDto(next.Id, next.Year, next.Month, next.Tariff, next.Amount, next.PaidAt,
            next.Source, true, next.RecordedBy));
    }

    public Task SaveContractAsync(int subscriberId, UpdateContractRequest request)
    {
        using var ctx = db.CreateDbContext();
        var s = ctx.Subscribers.First(x => x.Id == subscriberId);
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
        var h = ctx.Houses.First(x => x.Id == houseId);
        h.Antennas = request.Antennas ?? "";
        h.AerialCables = request.AerialCables ?? "";
        h.Amplifiers = request.Amplifiers ?? "";
        h.PowerSupplies = request.PowerSupplies ?? "";
        h.TechFloorKey = request.TechFloorKey ?? "";
        h.OtherNotes = request.OtherNotes ?? "";
        ctx.SaveChanges();
        return Task.CompletedTask;
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
}
