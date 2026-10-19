using System.Net;
using System.Net.Http.Json;
using Avers.Server.Hosting;

namespace Avers.Server.Tests;

public class AdminCommandsTests
{
    [Fact]
    public async Task SetPassword_ReplacesDemoPassword()
    {
        await using var app = new AversApp();

        var code = await AdminCommands.SetPasswordAsync(app.Services, "orlov", new StringReader("Новый-пароль-1\n"));

        Assert.Equal(0, code);
        var old = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { login = "orlov", password = "avers" });
        var fresh = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { login = "orlov", password = "Новый-пароль-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [Theory]
    [InlineData("nobody", "достаточно-длинный", 1)]
    [InlineData("orlov", "12345", 2)]
    public async Task SetPassword_RejectsUnknownUserAndShortPassword(string login, string password, int expected)
    {
        await using var app = new AversApp();

        Assert.Equal(expected, await AdminCommands.SetPasswordAsync(app.Services, login, new StringReader(password)));
    }
}
