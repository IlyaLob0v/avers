using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Domain;

namespace Avers.Server.Tests;

public class ChangeLogTests
{
    private static async Task<List<ChangeLogEntry>> LogAsync(AversApp app) =>
        (await (await app.LoginAsync("admin")).GetFromJsonAsync<List<ChangeLogEntry>>("/api/change-log"))!;

    [Fact]
    public async Task Log_OnlyAdministrator()
    {
        await using var app = new AversApp();

        var head = await (await app.LoginAsync("orlov")).GetAsync("/api/change-log");
        var admin = await (await app.LoginAsync("admin")).GetAsync("/api/change-log");

        Assert.Equal(HttpStatusCode.Forbidden, head.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task Payment_IsLogged()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");

        var paid = await (await client.PostAsJsonAsync("/api/subscribers/1/payments", new RecordPaymentRequest(2026)))
            .Content.ReadFromJsonAsync<PaymentDto>();

        var entry = Assert.Single(await LogAsync(app));
        Assert.Equal("Орлов С.А. (orlov)", entry.UserName);
        Assert.Equal($"Оплата за {paid!.Month:00}.2026", entry.Action);
        Assert.StartsWith("л/с ", entry.Target);
        Assert.Contains($"Сумма: — → {paid.Amount!.Value.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))}", entry.Changes);
        Assert.Contains("Источник: — → Касса", entry.Changes);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero), entry.At);
    }

    [Fact]
    public async Task Contract_LogsOnlyChangedFields_BeforeAndAfter()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("orlov");
        var card = (await client.GetFromJsonAsync<SubscriberCard>("/api/subscribers/1"))!;

        await client.PutAsJsonAsync("/api/subscribers/1/contract", new UpdateContractRequest(
            "ТО-2026/77", card.ContractDate, card.ContractTerminated, null, card.Notes));

        var entry = Assert.Single(await LogAsync(app));
        Assert.Equal("Договор и примечание", entry.Action);
        Assert.Equal($"Номер договора: {card.ContractNumber} → ТО-2026/77", entry.Changes);
    }

    [Fact]
    public async Task SaveWithoutChanges_IsNotLogged()
    {
        await using var app = new AversApp();
        var request = new HousePassportRequest("Антенна ДМВ", "РК-75", "Terra HA-126", "БП 12 В", "у старшего по дому", "");
        var engineer = await app.LoginAsync("engineer");

        await engineer.PutAsJsonAsync("/api/houses/1/passport", request);
        await engineer.PutAsJsonAsync("/api/houses/1/passport", request);

        var entry = Assert.Single(await LogAsync(app));
        Assert.Equal("Паспорт дома", entry.Action);
        Assert.Contains("Усилители: ", entry.Changes);
        Assert.Contains("→ Terra HA-126", entry.Changes);
    }

    [Fact]
    public async Task User_PasswordChangeIsLoggedWithoutValue()
    {
        await using var app = new AversApp();
        var admin = await app.LoginAsync("admin");

        await admin.PutAsJsonAsync("/api/users/5", new SaveUserRequest("orlova", "Орлова Т.И.", "AdsHead", "АДС, диспетчерская", false, false, "secret1"));

        var entry = Assert.Single(await LogAsync(app));
        Assert.Equal("Администратор (admin)", entry.UserName);
        Assert.Equal("Сотрудник orlova", entry.Target);
        Assert.Contains("Роль: Пользователь АДС (диспетчер) → Начальник АДС", entry.Changes);
        Assert.Contains("Пароль: изменён", entry.Changes);
        Assert.DoesNotContain("secret1", entry.Changes);
    }
}
