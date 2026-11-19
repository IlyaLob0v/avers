using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Avers.Contracts;
using Avers.Data;
using Microsoft.AspNetCore.Hosting;

namespace Avers.Server.Tests;

public sealed class WriteModeTests
{
    [Theory]
    [InlineData("ReadOnly", HttpStatusCode.Conflict)]
    [InlineData("Pilot", HttpStatusCode.OK)]
    public async Task PilotMode_AllowsOnlySelectedWork(string mode, HttpStatusCode paymentStatus)
    {
        await using var app = new AversApp();
        using var configured = app.WithWebHostBuilder(builder => builder.UseSetting("Avers:WriteMode", mode));
        using var client = configured.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "avers"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add(DesktopClient.Header, DesktopClient.Version.ToString());

        const string url = "/api/subscribers/1/payments";
        var month = (await client.GetFromJsonAsync<List<PaymentDto>>(url + "?year=2026"))!.First(p => !p.IsPaid);
        using var pay = await client.PostAsJsonAsync(url, new RecordPaymentRequest(2026, month.Month, OperationId: Guid.NewGuid()));
        Assert.Equal(paymentStatus, pay.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/accounting?year=2026&month=8")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/oek/import", new OekImportRequest(2026, 8, []))).StatusCode);
    }
}
