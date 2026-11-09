using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Avers.Server.Tests;

public class OpenApiTests
{
    /// <summary>Описание API для разработчика (docs/development.md) открывается в режиме Development.</summary>
    [Fact]
    public async Task Document_IsServedInDevelopment()
    {
        await using var app = new AversApp();
        await using var dev = app.WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var response = await dev.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/debts", await response.Content.ReadAsStringAsync());
    }
}
