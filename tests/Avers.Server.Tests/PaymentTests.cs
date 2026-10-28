using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;

namespace Avers.Server.Tests;

public class PaymentTests
{
    [Fact]
    public async Task Record_Defaults_PaysFirstUnpaidMonthByTariff()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var before = await client.GetFromJsonAsync<List<PaymentDto>>("/api/subscribers/1/payments?year=2026");
        var firstUnpaid = before!.First(p => !p.IsPaid);

        var response = await client.PostAsJsonAsync("/api/subscribers/1/payments", new RecordPaymentRequest(2026));

        response.EnsureSuccessStatusCode();
        var paid = await response.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.Equal(firstUnpaid.Month, paid!.Month);
        Assert.Equal(firstUnpaid.Tariff, paid.Amount);
        Assert.Equal(AversApp.Today, paid.PaidAt);
        Assert.Equal("Касса", paid.Source);

        var card = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/1");
        Assert.Equal(AversApp.Today, card!.LastPaymentDate);
    }

    [Fact]
    public async Task Record_SameMonthTwice_Returns409()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        var month = (await client.GetFromJsonAsync<List<PaymentDto>>("/api/subscribers/1/payments?year=2026"))!
            .First(p => !p.IsPaid).Month;

        var first = await client.PostAsJsonAsync("/api/subscribers/1/payments", new RecordPaymentRequest(2026, month));
        var second = await client.PostAsJsonAsync("/api/subscribers/1/payments", new RecordPaymentRequest(2026, month));

        first.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Record_ZeroAmount_CountsAsPaidMonth()
    {
        // Так в текущем коде; вопрос к бухгалтерии открыт (docs/07-open-questions.md).
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        var before = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");

        var response = await client.PostAsJsonAsync("/api/subscribers/3/payments", new RecordPaymentRequest(2026, Amount: 0m));

        response.EnsureSuccessStatusCode();
        var after = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");
        Assert.Equal(before!.UnpaidMonths - 1, after!.UnpaidMonths);
        Assert.Equal(before.Debt, after.Debt);
    }

    [Theory]
    [InlineData(-1.0, null)]
    [InlineData(10.555, null)]
    [InlineData(null, 13)]
    public async Task Record_InvalidRequest_Returns400(double? amount, int? month)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var response = await client.PostAsJsonAsync("/api/subscribers/1/payments",
            new RecordPaymentRequest(2026, month, (decimal?)amount));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("2026-09-01")]
    [InlineData("1999-12-31")]
    public async Task Record_PaidAtOutOfRange_Returns400(string paidAt)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var response = await client.PostAsJsonAsync("/api/subscribers/1/payments",
            new RecordPaymentRequest(2026, PaidAt: DateOnly.Parse(paidAt, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Record_YearWithoutCharges_Returns409()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var response = await client.PostAsJsonAsync("/api/subscribers/1/payments", new RecordPaymentRequest(2019, 5));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
