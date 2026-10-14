using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data.Tests;

public class DemoSeederTests
{
    [Fact]
    public void EnsureSeeded_CreatesUsersHousesAndSubscribers()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
            DemoSeeder.EnsureSeeded(db);

        using var check = database.CreateContext();
        var roles = check.Users.Select(u => u.Role).Distinct().ToList();

        Assert.Contains(UserRole.Administrator, roles);
        Assert.Contains(UserRole.Dispatcher, roles);
        Assert.Contains(UserRole.Reader, roles);
        Assert.True(check.Subscribers.Any());
        Assert.True(check.Houses.Any());
    }

    [Fact]
    public void EnsureSeeded_RunsOnlyOnce()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
            DemoSeeder.EnsureSeeded(db);

        int users, subscribers;
        using (var db = database.CreateContext())
        {
            users = db.Users.Count();
            subscribers = db.Subscribers.Count();
            DemoSeeder.EnsureSeeded(db);
        }

        using var check = database.CreateContext();
        Assert.Equal(users, check.Users.Count());
        Assert.Equal(subscribers, check.Subscribers.Count());
    }

    [Fact]
    public void EnsureSeeded_CreatesTwelveMonthsPerSubscriber()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
            DemoSeeder.EnsureSeeded(db);

        using var check = database.CreateContext();
        var months = check.Subscribers
            .Select(s => new { s.Account, Count = s.Payments.Count(p => p.Year == 2026) })
            .Where(x => x.Count > 0)
            .ToList();

        Assert.NotEmpty(months);
        Assert.All(months, x => Assert.Equal(12, x.Count));
    }

    [Fact]
    public void DemoAdmin_CanSignInWithDefaultPassword()
    {
        using var database = new SqliteDatabase();
        using (var db = database.CreateContext())
            DemoSeeder.EnsureSeeded(db);

        using var check = database.CreateContext();
        var admin = check.Users.AsNoTracking().Single(u => u.Login == "admin");

        Assert.True(Password.Verify(admin, "avers"));
        Assert.False(Password.Verify(admin, "wrong"));
    }
}
