using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Microsoft.EntityFrameworkCore;

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
    public async Task ChangeTariff_ForOneApartment_OnlyUnpaidMonths_LawyerIsForbidden()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");
        var head = await app.LoginAsync("orlov");
        var request = new TariffChangeRequest(2026, 1, 120m, null, 3);

        var forbidden = await lawyer.PutAsJsonAsync("/api/tariff", request);
        var result = await (await head.PutAsJsonAsync("/api/tariff", request)).Content.ReadFromJsonAsync<TariffChangeResult>();
        var payments = await head.GetFromJsonAsync<List<PaymentDto>>("/api/subscribers/3/payments?year=2026");
        var other = await head.GetFromJsonAsync<List<PaymentDto>>("/api/subscribers/1/payments?year=2026");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(payments!.Count(p => !p.IsPaid), result!.Months);
        Assert.All(payments!, p => Assert.Equal(p.IsPaid ? 95m : 120m, p.Tariff));
        Assert.DoesNotContain(other!, p => p.Tariff == 120m);
    }

    [Fact]
    public async Task AddApartment_CreatesSubscriber_LawyerIsForbidden()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");
        var head = await app.LoginAsync("orlov");
        var request = new NewApartmentRequest(1, null, "99", "990099", "Смирнов", "Олег", null, null, "ТО-2026/990099",
            new DateOnly(2026, 8, 10), 95m);

        var forbidden = await lawyer.PostAsJsonAsync("/api/subscribers", request);
        var created = await head.PostAsJsonAsync("/api/subscribers", request);
        var duplicate = await head.PostAsJsonAsync("/api/subscribers", request);
        var found = await head.GetFromJsonAsync<List<SubscriberListItem>>("/api/subscribers?mode=Account&q=990099");
        var payments = await head.GetFromJsonAsync<List<PaymentDto>>($"/api/subscribers/{found!.Single().Id}/payments?year=2026");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal([8, 9, 10, 11, 12], payments!.Select(p => p.Month));
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
    public async Task CourtCase_LawyerAddsAndEdits_AdsHeadIsForbidden()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");
        var head = await app.LoginAsync("orlov");
        var request = new CourtCaseRequest("2-9/2026", new DateOnly(2026, 8, 20), "Мировой судья с/у № 47", 475m, "Подано",
            200m, "ОСФР", new DateOnly(2026, 8, 25), "100,00-30.08.26", "Пенсионер");

        var forbidden = await head.PostAsJsonAsync("/api/subscribers/3/court-cases", request);
        var created = await lawyer.PostAsJsonAsync("/api/subscribers/3/court-cases", request);
        var caseId = await app.WithDbAsync(db => db.CourtCases.Where(c => c.SubscriberId == 3).Select(c => c.Id).SingleAsync());
        var edited = await lawyer.PutAsJsonAsync($"/api/subscribers/3/court-cases/{caseId}", request with { Status = "Выдан" });
        var debts = await lawyer.GetFromJsonAsync<Avers.Data.Debts>("/api/debts");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        var saved = await app.WithDbAsync(db => db.CourtCases.SingleAsync(c => c.Id == caseId));
        Assert.Equal(("Выдан", 200m, "ОСФР", (DateOnly?)new DateOnly(2026, 8, 25), "100,00-30.08.26", "Пенсионер"),
            (saved.Status, saved.StateDuty, saved.WritSentTo, saved.WritSentDate, saved.Receipts, saved.Notes));
        Assert.Contains(debts!.Debtors, d => d.Case is { OrderNumber: "2-1140/2026" });
    }

    [Fact]
    public async Task CancelPayment_OnlyAdminAndAdsHead_MonthBecomesUnpaid()
    {
        await using var app = new AversApp();
        var head = await app.LoginAsync("orlov");
        var user = await app.LoginAsync("sokolova");
        var paid = await (await head.PostAsJsonAsync("/api/subscribers/3/payments", new RecordPaymentRequest(2026)))
            .Content.ReadFromJsonAsync<PaymentDto>();

        var forbidden = await user.DeleteAsync($"/api/subscribers/3/payments/{paid!.Id}");
        var cancelled = await head.DeleteAsync($"/api/subscribers/3/payments/{paid.Id}");
        var again = await head.DeleteAsync($"/api/subscribers/3/payments/{paid.Id}");
        var card = await head.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(new DateOnly(2026, 5, 22), card!.LastPayment!.PaidAt);
    }

    [Fact]
    public async Task UpdateContract_ByAdsHead_IsSaved()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var response = await client.PutAsJsonAsync("/api/subscribers/3/contract", new UpdateContractRequest(
            "ТО-2026/77", new DateOnly(2026, 9, 1), null, "Петрова Анна Ивановна", "Новый собственник", "коллективная оплата"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var card = await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/3");
        Assert.Equal("ТО-2026/77", card!.ContractNumber);
        Assert.Equal("Петрова Анна Ивановна", card.ContractParty);
        Assert.Equal("Новый собственник", card.Notes);
        Assert.Equal(new DateOnly(2026, 9, 1), card.ContractDate);
        Assert.Equal("коллективная оплата", card.Benefit);
    }

    [Fact]
    public async Task UpdateContract_Termination_StopsAccrualsAfterItsMonth()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        await client.PutAsJsonAsync("/api/subscribers/3/contract", new UpdateContractRequest(
            "ТО-2020/3", new DateOnly(2020, 1, 1), new DateOnly(2026, 6, 15), null, null));
        var payments = await client.GetFromJsonAsync<List<PaymentDto>>("/api/subscribers/3/payments?year=2026");

        Assert.DoesNotContain(payments!, p => p.Month > 6 && !p.IsPaid);
        Assert.Contains(payments!, p => p.Month == 6);
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
        var unknownBenefit = await client.PutAsJsonAsync("/api/subscribers/1/contract",
            new UpdateContractRequest("1", null, null, null, null, "ветеран труда"));
        var missing = await client.PutAsJsonAsync("/api/subscribers/999/contract",
            new UpdateContractRequest("1", null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDates.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownBenefit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
