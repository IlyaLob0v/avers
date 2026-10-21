using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class UserTests
{
    private static SaveUserRequest NewUser(string? password = "secret1", bool disabled = false, string role = "Dispatcher") =>
        new("petrova", "Петрова А.А.", role, "АДС", false, disabled, password);

    [Fact]
    public async Task Users_OnlyAdministrator()
    {
        await using var app = new AversApp();

        var head = await (await app.LoginAsync("orlov")).GetAsync("/api/users");
        var users = await (await app.LoginAsync("admin")).GetFromJsonAsync<List<UserDto>>("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, head.StatusCode);
        Assert.Contains(users!, u => u.Login == "orlova" && u.Role == "Dispatcher");
    }

    [Fact]
    public async Task Create_NewEmployeeCanLogIn()
    {
        await using var app = new AversApp();
        var admin = await app.LoginAsync("admin");

        var created = await admin.PostAsJsonAsync("/api/users", NewUser());
        var login = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("petrova", "secret1"));

        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutPasswordOrWithTakenLogin_Returns400()
    {
        await using var app = new AversApp();
        var admin = await app.LoginAsync("admin");

        var noPassword = await admin.PostAsJsonAsync("/api/users", NewUser(password: null));
        var taken = await admin.PostAsJsonAsync("/api/users", NewUser() with { Login = "orlova" });

        Assert.Equal(HttpStatusCode.BadRequest, noPassword.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
    }

    [Fact]
    public async Task Edit_NewPasswordReplacesOld_EmptyKeepsIt()
    {
        await using var app = new AversApp();
        var admin = await app.LoginAsync("admin");
        var id = await IdOf(app, "orlova");
        var orlova = new SaveUserRequest("orlova", "Орлова Т.И.", "AdsHead", "АДС", true, false, "");

        await admin.PutAsJsonAsync($"/api/users/{id}", orlova);
        var oldPassword = await LoginStatus(app, "orlova", "avers");
        await admin.PutAsJsonAsync($"/api/users/{id}", orlova with { Password = "newpass" });

        Assert.Equal(HttpStatusCode.OK, oldPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(app, "orlova", "avers"));
        Assert.Equal(HttpStatusCode.OK, await LoginStatus(app, "orlova", "newpass"));
        var role = await app.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.Role).SingleAsync());
        Assert.Equal(Domain.UserRole.AdsHead, role);
    }

    [Fact]
    public async Task Disabled_CannotLogIn_ButKeepsTickets()
    {
        await using var app = new AversApp();
        var admin = await app.LoginAsync("admin");
        var id = await IdOf(app, "orlova");
        var tickets = await app.WithDbAsync(db => db.Tickets.CountAsync(t => t.AcceptedByUserId == id));

        var response = await admin.PutAsJsonAsync($"/api/users/{id}",
            new SaveUserRequest("orlova", "Орлова Т.И.", "Dispatcher", "АДС, диспетчерская", false, true, ""));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(app, "orlova", "avers"));
        Assert.Equal(tickets, await app.WithDbAsync(db => db.Tickets.CountAsync(t => t.AcceptedByUserId == id)));
    }

    [Theory]
    [InlineData("Administrator", true)]
    [InlineData("Dispatcher", false)]
    public async Task Administrator_CannotDisableSelfOrDropOwnRole(string role, bool disabled)
    {
        await using var app = new AversApp();
        var admin = await app.LoginAsync("admin");
        var id = await IdOf(app, "admin");

        var response = await admin.PutAsJsonAsync($"/api/users/{id}",
            new SaveUserRequest("admin", "Администратор", role, "Служба АИТ", true, disabled, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginStatus(app, "admin", "avers"));
    }

    [Fact]
    public async Task DisableAndRoleChange_ApplyToOpenSession()
    {
        await using var app = new AversApp();
        var admin = await app.LoginAsync("admin");
        var orlova = await app.LoginAsync("orlova");
        var id = await IdOf(app, "orlova");
        var request = new SaveUserRequest("orlova", "Орлова Т.И.", "AdsHead", "АДС", false, false, "");

        var before = await orlova.PutAsJsonAsync("/api/settings/payment-warning", new PaymentWarningSettings(3));
        await admin.PutAsJsonAsync($"/api/users/{id}", request);
        var promoted = await orlova.PutAsJsonAsync("/api/settings/payment-warning", new PaymentWarningSettings(3));
        await admin.PutAsJsonAsync($"/api/users/{id}", request with { IsDisabled = true });
        var disabled = await orlova.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
    }

    private static Task<int> IdOf(AversApp app, string login) =>
        app.WithDbAsync(db => db.Users.Where(u => u.Login == login).Select(u => u.Id).SingleAsync());

    private static async Task<HttpStatusCode> LoginStatus(AversApp app, string login, string password) =>
        (await app.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(login, password))).StatusCode;
}
