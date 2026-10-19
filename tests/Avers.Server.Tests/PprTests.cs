using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class PprTests
{
    [Fact]
    public async Task Mark_ByChiefEngineerOnly()
    {
        await using var app = new AversApp();
        var request = new PprMarkRequest(nameof(PprStatus.Planned));

        var head = await (await app.LoginAsync("orlov")).PutAsJsonAsync("/api/ppr/1/2026/11", request);
        var engineer = await (await app.LoginAsync("engineer")).PutAsJsonAsync("/api/ppr/1/2026/11", request);

        Assert.Equal(HttpStatusCode.Forbidden, head.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, engineer.StatusCode);
        var status = await app.WithDbAsync(db => db.PprEntries.Where(e => e.HouseId == 1 && e.Year == 2026 && e.Month == 11)
            .Select(e => e.Status).SingleAsync());
        Assert.Equal(PprStatus.Planned, status);
    }

    [Fact]
    public async Task MarkDone_MovesLastPprOfTheHouse()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");

        var response = await client.PutAsJsonAsync("/api/ppr/1/2026/8", new PprMarkRequest(nameof(PprStatus.Done)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var last = await app.WithDbAsync(db => db.Houses.Where(h => h.Id == 1).Select(h => h.LastPpr).SingleAsync());
        Assert.Equal(AversApp.Today, last);
    }

    [Theory]
    [InlineData(13, "Planned")]
    [InlineData(5, "Overdue")]
    [InlineData(5, "сделано")]
    public async Task Mark_RejectsBadMonthOrStatus(int month, string status)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");

        var response = await client.PutAsJsonAsync($"/api/ppr/1/2026/{month}", new PprMarkRequest(status));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Copy_MovesPlanToNextYearOnce()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");

        var first = await client.PostAsync("/api/ppr/2026/copy", null);
        var second = await client.PostAsync("/api/ppr/2026/copy", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = await first.Content.ReadFromJsonAsync<PprCopyResult>();
        var planned = await app.WithDbAsync(db => db.PprEntries.CountAsync(e => e.Year == 2026 && e.Status != PprStatus.Empty));
        Assert.Equal(new PprCopyResult(2027, planned), result);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
