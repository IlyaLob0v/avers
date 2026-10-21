using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;

namespace Avers.Server.Tests;

public class ApartmentCardTests
{
    // Учебная база: 1 — Иванова (оплата 09.07.2026), 3 — Петров (оплата 22.05.2026), 2 — Белоусов.

    [Fact]
    public async Task Card_ShowsContractAndLastPayment()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var card = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/1");

        Assert.Equal("ТО-2014/1932068", card!.ContractNumber);
        Assert.Equal("Иванова Мария Петровна", card.ContractParty);
        Assert.Equal("ООО «Жилсервис-Центр», Филиал Советский", card.ServiceOrganization);
        Assert.Equal(new DateOnly(2026, 7, 9), card.LastPayment!.PaidAt);
        Assert.Equal("Импорт реестра ОЭК", card.LastPayment.RecordedBy);
        Assert.Equal("Иванов Павел Сергеевич", card.LastPayment.PayerName);
        Assert.Equal(1, card.MonthsSinceLastPayment);
        Assert.False(card.IsPaymentLate);
        Assert.Equal(2, card.PaymentWarningMonths);
    }

    [Fact]
    public async Task Card_ContractWithAnotherPerson()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var card = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/2");

        Assert.Equal("Белоусова Галина Ивановна", card!.ContractParty);
    }

    [Fact]
    public async Task Card_ThreeMonthsWithoutPayment_IsLate_UntilThresholdRaised()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var before = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");
        var put = await client.PutAsJsonAsync("/api/settings/payment-warning", new PaymentWarningSettings(4));
        var after = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");

        Assert.Equal(3, before!.MonthsSinceLastPayment);
        Assert.True(before.IsPaymentLate);
        put.EnsureSuccessStatusCode();
        Assert.False(after!.IsPaymentLate);
        Assert.Equal(4, after.PaymentWarningMonths);
    }

    [Fact]
    public async Task Settings_LawyerCannotChange_AndRangeIsChecked()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");
        var admin = await app.LoginAsync("admin");

        var read = await lawyer.GetFromJsonAsync<PaymentWarningSettings>("/api/settings/payment-warning");
        var forbidden = await lawyer.PutAsJsonAsync("/api/settings/payment-warning", new PaymentWarningSettings(5));
        var invalid = await admin.PutAsJsonAsync("/api/settings/payment-warning", new PaymentWarningSettings(0));

        Assert.Equal(2, read!.Months);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task OpenYear_NextYearMonthsAppear_OnlyForSettingsEditors()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");
        var admin = await app.LoginAsync("admin");

        var forbidden = await lawyer.PostAsync("/api/accruals/2027", null);
        var tooFar = await admin.PostAsync("/api/accruals/2028", null);
        var opened = await (await admin.PostAsync("/api/accruals/2027", null)).Content.ReadFromJsonAsync<OpenYearResult>();
        var again = await (await admin.PostAsync("/api/accruals/2027", null)).Content.ReadFromJsonAsync<OpenYearResult>();
        var payments = await admin.GetFromJsonAsync<List<PaymentDto>>("/api/subscribers/1/payments?year=2027");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);
        Assert.True(opened!.Subscribers > 0);
        Assert.Equal(0, again!.Subscribers);
        Assert.Equal(12, payments!.Count);
        Assert.All(payments, p => Assert.False(p.IsPaid));
    }

    [Fact]
    public async Task RecordPayment_RemembersWhoRecordedAndWhoPaid()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var response = await client.PostAsJsonAsync("/api/subscribers/3/payments",
            new RecordPaymentRequest(2026, PayerName: "Петрова Анна Ивановна"));

        response.EnsureSuccessStatusCode();
        var paid = await response.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.Equal("Орлов С.А.", paid!.RecordedBy);
        Assert.Equal("Петрова Анна Ивановна", paid.PayerName);

        var card = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");
        Assert.Equal(paid.Month, card!.LastPayment!.Month);
        Assert.Equal(0, card.MonthsSinceLastPayment);
        Assert.False(card.IsPaymentLate);
    }

    [Fact]
    public async Task UpdateContract_ByAdsHead_IsSaved()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var response = await client.PutAsJsonAsync("/api/subscribers/3/contract", new UpdateContractRequest(
            "ТО-2026/77", new DateOnly(2026, 9, 1), null, "Петрова Анна Ивановна", "Новый собственник"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var card = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");
        Assert.Equal("ТО-2026/77", card!.ContractNumber);
        Assert.Equal("Петрова Анна Ивановна", card.ContractParty);
        Assert.Equal("Новый собственник", card.Notes);
        Assert.Equal(new DateOnly(2026, 9, 1), card.ContractDate);
    }

    [Fact]
    public async Task UpdateContract_LawyerIsForbidden()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("lawyer");

        var response = await client.PutAsJsonAsync("/api/subscribers/1/contract",
            new UpdateContractRequest("1", null, null, null, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContract_Validates()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");

        var tooLong = await client.PutAsJsonAsync("/api/subscribers/1/contract",
            new UpdateContractRequest(new string('1', 41), null, null, null, null));
        var badDates = await client.PutAsJsonAsync("/api/subscribers/1/contract",
            new UpdateContractRequest("1", new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 1), null, null));
        var missing = await client.PutAsJsonAsync("/api/subscribers/999/contract",
            new UpdateContractRequest("1", null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDates.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
