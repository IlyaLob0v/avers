using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Data;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Tests;

public class DocumentTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n%%EOF"u8.ToArray();

    [Fact]
    public async Task LawyerUploadsContract_OpensAndDeletes_Logged()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");

        var created = await lawyer.PostAsJsonAsync("/api/subscribers/3/documents", new AddDocumentRequest("Договор", "Договор 1234", Pdf));
        var doc = (await lawyer.GetFromJsonAsync<List<DocumentInfo>>("/api/subscribers/3/documents"))!.Single();
        var file = await lawyer.GetAsync($"/api/documents/{doc.Id}/file");
        var deleted = await lawyer.DeleteAsync($"/api/documents/{doc.Id}");

        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        Assert.Equal(("Договор", "Договор 1234", "PDF", AversApp.Today), (doc.Kind, doc.Name, doc.Format, doc.AddedAt));
        Assert.Equal("application/pdf", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Pdf, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await lawyer.GetFromJsonAsync<List<DocumentInfo>>("/api/subscribers/3/documents"))!);
        Assert.Equal(2, await app.WithDbAsync(db => db.ChangeLog.CountAsync(e => e.Action == "Скан документа")));
    }

    [Fact]
    public async Task NotAScan_IsRejected()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");

        var response = await lawyer.PostAsJsonAsync("/api/subscribers/3/documents", new AddDocumentRequest("Договор", "Договор", "PK\u0003\u0004"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dispatcher_CannotSeeOrUpload()
    {
        await using var app = new AversApp();
        var dispatcher = await app.LoginAsync("orlova");
        var head = await app.LoginAsync("orlov");

        var list = await dispatcher.GetAsync("/api/subscribers/3/documents");
        var upload = await head.PostAsJsonAsync("/api/subscribers/3/documents", new AddDocumentRequest("Договор", "Договор", Pdf));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }
}
