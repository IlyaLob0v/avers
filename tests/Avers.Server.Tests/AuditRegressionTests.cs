using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Desktop;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Avers.Server.Tests;

public class AuditRegressionTests
{
    [Fact]
    public async Task PhonebookConflict_StaleDeleteDoesNotRemoveChangedOrganization()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");
        var request = new OrganizationRequest("Организация для проверки", "", "", "", "", "", "", "");
        (await client.PostAsJsonAsync("/api/organizations", request)).EnsureSuccessStatusCode();
        var id = await app.WithDbAsync(db => db.Organizations.Where(o => o.Name == request.Name).Select(o => o.Id).SingleAsync());
        (await client.PutAsJsonAsync($"/api/organizations/{id}", request with { Phones = "Новый телефон" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/organizations/{id}?revision=0")).StatusCode);
        Assert.True(await app.WithDbAsync(db => db.Organizations.AnyAsync(o => o.Id == id)));
    }

    [Fact]
    public async Task PhonebookConflict_CannotOverwriteOrganizationChangedInPassport()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");
        var house = await app.WithDbAsync(db => db.Houses.Include(h => h.Organization).FirstAsync(h => h.OrganizationId != null));
        var org = house.Organization!;
        var passport = new HousePassportRequest("", "", "", "", "", "",
            Organization: new HouseOrganizationRequest(org.Name, org.Branch, org.Address, "Новый телефон", org.HeadName, org.EngineerName),
            Revision: house.Revision, OrganizationRevision: org.Revision);
        (await client.PutAsJsonAsync($"/api/houses/{house.Id}/passport", passport)).EnsureSuccessStatusCode();
        var response = await client.PutAsJsonAsync($"/api/organizations/{org.Id}", new
        {
            org.Name,
            org.Branch,
            org.Address,
            org.Phones,
            org.HeadName,
            org.HeadPhone,
            org.EngineerName,
            org.EngineerPhone,
            org.Revision
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Новый телефон", await app.WithDbAsync(db => db.Organizations.Where(o => o.Id == org.Id).Select(o => o.Phones).SingleAsync()));
    }

    [Fact]
    public async Task PhonebookConflict_RejectsStaleHouse()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");
        var request = new HouseRequest(1, "1", "", "", 5, 2, 2, 20, "", null, true);
        (await client.PutAsJsonAsync("/api/houses/1", request)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/houses/1", request with { Floors = 9 })).StatusCode);
        Assert.Equal(5, await app.WithDbAsync(db => db.Houses.Where(h => h.Id == 1).Select(h => h.Floors).SingleAsync()));
    }

    [Fact]
    public async Task LateCancellation_CannotCancelIdenticalReplacementReceipt()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        var first = await (await client.PostAsJsonAsync("/api/subscribers/3/payments", new RecordPaymentRequest(2026)))
            .Content.ReadFromJsonAsync<PaymentDto>();
        (await client.DeleteAsync($"/api/subscribers/3/payments/{first!.Id}?revision={first.Revision}")).EnsureSuccessStatusCode();
        var replacement = await (await client.PostAsJsonAsync("/api/subscribers/3/payments", new RecordPaymentRequest(2026, first.Month, first.Amount)))
            .Content.ReadFromJsonAsync<PaymentDto>();
        var stale = await client.DeleteAsync($"/api/subscribers/3/payments/{first.Id}?revision={first.Revision}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var saved = await app.WithDbAsync(db => db.Payments.AsNoTracking().SingleAsync(p => p.Id == first.Id));
        Assert.Equal(replacement!.Amount, saved.Amount);
        Assert.Equal(replacement.Revision, saved.Revision);
    }

    [Fact]
    public async Task ContractTermination_ConflictsWithPayment_WithoutLosingMoney()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        await app.WithDbAsync(async db =>
        {
            var month = await db.Payments.FirstAsync(p => p.SubscriberId == 3 && p.Year == 2026 && p.Amount == null && p.Month > 1);
            var subscriber = await db.Subscribers.SingleAsync(s => s.Id == 3);
            (await client.PostAsJsonAsync("/api/subscribers/3/payments", new RecordPaymentRequest(2026, month.Month, 120))).EnsureSuccessStatusCode();
            Assert.Throws<DbUpdateConcurrencyException>(() => SubscriberContracts.Save(db, subscriber,
                new Subscriber { ContractTerminated = new DateOnly(2026, month.Month, 1).AddDays(-1) }, AversApp.Today, "Админ", DateTimeOffset.UtcNow));
            return 0;
        });
        Assert.True(await app.WithDbAsync(db => db.Payments.AnyAsync(p => p.SubscriberId == 3 && p.Amount == 120)));
    }

    [Fact]
    public async Task Payment_RejectsAmountOutsideDatabasePrecision()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("admin");
        var response = await client.PostAsJsonAsync("/api/subscribers/3/payments", new RecordPaymentRequest(2026, Amount: 10_000_000_000m));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LawyerSnapshot_HasAddressesWithoutTechnicalData()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("lawyer");
        var snapshot = (await client.GetFromJsonAsync<AversSnapshot>("/api/desktop/snapshot"))!.Link();
        Assert.NotEmpty(snapshot.Houses);
        Assert.All(snapshot.Houses, h =>
        {
            Assert.NotNull(h.Street);
            Assert.Empty(h.TechFloorKey);
            Assert.Empty(h.Antennas);
            Assert.Empty(h.OtherNotes);
            Assert.Null(h.OpenFault);
        });
        Assert.Empty(snapshot.Schemes);
        Assert.Empty(snapshot.Ppr);
        Assert.Empty(snapshot.Intercoms);
        Assert.Empty(snapshot.Organizations);
    }

    [Fact]
    public async Task Passport_RejectsStaleDraft_InsteadOfOverwritingColleague()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");
        var request = new { antennas = "Первый сотрудник", amplifiers = "", revision = 0 };
        (await client.PutAsJsonAsync("/api/houses/1/passport", request)).EnsureSuccessStatusCode();
        var stale = await client.PutAsJsonAsync("/api/houses/1/passport", new { antennas = "Старые сведения", amplifiers = "Второй сотрудник", revision = 0 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("Первый сотрудник", await app.WithDbAsync(db => Task.FromResult(db.Houses.Single(h => h.Id == 1).Antennas)));
    }

    [Fact]
    public async Task SchemeUpload_RejectsNullAndTruncatedImage()
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync("engineer");
        foreach (var content in new byte[]?[] { null, [0x89, 0x50, 0x4E, 0x47] })
        {
            var response = await client.PostAsJsonAsync("/api/houses/1/schemes", new { name = "Повреждённый файл", content });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public void DocumentValidation_RejectsNullAndTruncatedPdf()
    {
        Assert.NotNull(SubscriberDocuments.Check("Договор", "Скан", null!));
        Assert.NotNull(SubscriberDocuments.Check("Договор", "Скан", "%PDF"u8.ToArray()));
    }

    [Fact]
    public void ImagePixelLimit_IsCheckedBeforeUploadOrWatermarkDecode()
    {
        using var bitmap = new SKBitmap(4096, 4096);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        var content = encoded.ToArray();
        Assert.True(content.Length < SchemeFiles.MaxBytes);
        Assert.NotNull(SchemeFiles.Check(content));
        Assert.NotNull(SubscriberDocuments.Check("Договор", "Скан", content));
        Assert.Null(SchemeWatermark.Apply(content, "PNG", "Тест"));
    }
}
