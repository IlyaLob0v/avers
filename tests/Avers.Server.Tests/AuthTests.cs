using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avers.Contracts;
using Avers.Server.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

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
    public async Task LoginLimit_CountsEachAddressBehindProxySeparately()
    {
        await using var app = new AversApp();
        var client = app.CreateClient();

        async Task<HttpStatusCode> Login(string address)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest("admin", "wrong"))
            };
            request.Headers.Add("X-Forwarded-For", address);
            return (await client.SendAsync(request)).StatusCode;
        }

        for (var i = 0; i < 10; i++)
            await Login("203.0.113.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, await Login("203.0.113.1"));
        Assert.Equal(HttpStatusCode.Unauthorized, await Login("203.0.113.2"));
    }

    [Fact]
    public async Task LoginLimit_LocksLoginAfterWrongPasswordsFromAnyAddress()
    {
        await using var app = new AversApp();
        var client = app.CreateClient();

        async Task<HttpStatusCode> Login(string login, string password, string address)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(login, password))
            };
            request.Headers.Add("X-Forwarded-For", address);
            return (await client.SendAsync(request)).StatusCode;
        }

        for (var i = 0; i < LoginFailures.Limit; i++)
            await Login("admin", "wrong", $"203.0.113.{i}");

        Assert.Equal(HttpStatusCode.TooManyRequests, await Login("admin", "avers", "198.51.100.1"));
        Assert.Equal(HttpStatusCode.OK, await Login("orlov", "avers", "198.51.100.1"));
    }

    private static async Task<HttpStatusCode> LoginFromInternet(HttpClient client, string login, string password, string address = "203.0.113.1")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(login, password))
        };
        request.Headers.Add(AuthEndpoints.InternetHeader, "1");
        request.Headers.Add("X-Forwarded-For", address);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task InternetLogin_OnlyReader()
    {
        await using var app = new AversApp();
        var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, await LoginFromInternet(client, "admin", "avers"));
        Assert.Equal(HttpStatusCode.OK, await LoginFromInternet(client, "novikov", "avers"));
    }

    [Fact]
    public async Task InternetWrongPasswords_DoNotLockOfficeLogin()
    {
        await using var app = new AversApp();
        var client = app.CreateClient();

        for (var i = 0; i < LoginFailures.Limit; i++)
            await LoginFromInternet(client, "orlov", "wrong", $"203.0.113.{i}");

        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginFromInternet(client, "orlov", "avers"));
        var office = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("orlov", "avers"));
        Assert.Equal(HttpStatusCode.OK, office.StatusCode);
    }

    [Fact]
    public async Task Login_SurvivesServerRestart_WhenKeysDirectoryIsSet()
    {
        var keys = Directory.CreateTempSubdirectory("avers-keys-").FullName;
        await using var app = new AversApp();
        var before = app.WithWebHostBuilder(b => b.UseSetting("Avers:KeysDirectory", keys));
        var login = await before.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "avers"));
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        var after = app.WithWebHostBuilder(b => b.UseSetting("Avers:KeysDirectory", keys)).CreateClient();
        after.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await after.GetAsync("/api/auth/me");

        Assert.NotEmpty(Directory.GetFiles(keys, "key-*.xml"));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Directory.Delete(keys, true);
    }

    [Theory]
    [InlineData("{\"login\":null,\"password\":\"avers\"}")]
    [InlineData("{\"login\":\"admin\",\"password\":null}")]
    [InlineData("{\"login\":\"  \",\"password\":\"avers\"}")]
    public async Task Login_EmptyFields_Returns401NotServerError(string body)
    {
        await using var app = new AversApp();
        var response = await app.CreateClient().PostAsync("/api/auth/login",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_OldHash_IsReplacedWithPbkdf2()
    {
        await using var app = new AversApp();
        var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("admin:avers:avers")));
        await app.WithDbAsync(db => db.Users.Where(u => u.Login == "admin")
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordHash, legacy)));

        await app.LoginAsync("admin");

        var stored = await app.WithDbAsync(db => db.Users.Where(u => u.Login == "admin").Select(u => u.PasswordHash).SingleAsync());
        Assert.StartsWith("pbkdf2.", stored);
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
