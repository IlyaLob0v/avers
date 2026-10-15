using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;

namespace Avers.Server.Tests;

public class AuthTests
{
    [Fact]
    public async Task Health_IsOpenWithoutLogin()
    {
        await using var app = new AversApp();
        var response = await app.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await using var app = new AversApp();
        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsRoleAndPaymentRight()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");

        Assert.Equal("AdsHead", me!.Role);
        Assert.Equal("Начальник АДС", me.RoleTitle);
        Assert.True(me.CanRecordPayment);
    }

    [Fact]
    public async Task Api_WithoutToken_Returns401()
    {
        await using var app = new AversApp();
        var response = await app.CreateClient().GetAsync("/api/streets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubscriberCard_DispatcherSeesApartmentData()
    {
        // Пользователь АДС видит данные квартиры (л/с, ФИО, договор) при приёме заявки — правки АДС, октябрь 2026.
        await using var app = new AversApp();
        var client = await app.LoginAsync("sokolova");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/subscribers/1")).StatusCode);
    }

    [Theory]
    [InlineData("novikov")]  // читатель (радиомеханик)
    public async Task SubscriberCard_ForbiddenWithoutAbonModule(string login)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync(login);

        var response = await client.GetAsync("/api/subscribers/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("lawyer")]
    [InlineData("engineer")]
    public async Task RecordPayment_ForbiddenForViewOnlyRoles(string login)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync(login);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/subscribers/1")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/subscribers/1/payments", new RecordPaymentRequest(2026));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
