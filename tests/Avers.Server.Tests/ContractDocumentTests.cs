using System.Net;
using System.Net.Http.Json;
using Avers.Contracts;
using Avers.Data;

namespace Avers.Server.Tests;

public class ContractDocumentTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n%%EOF"u8.ToArray();

    [Theory]
    [InlineData("orlov")]
    [InlineData("lawyer")]
    public async Task ContractEditors_CanUploadReadAndDelete(string login)
    {
        await using var app = new AversApp();
        var client = await app.LoginAsync(login);
        var upload = await client.PostAsJsonAsync("/api/subscribers/3/contract-documents", new AddDocumentRequest("Договор", "Договор № 123", Pdf));
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        var documents = await client.GetFromJsonAsync<List<DocumentInfo>>("/api/subscribers/3/contract-documents");
        var document = Assert.Single(documents!);
        Assert.Equal("Договор", document.Kind);
        var file = await client.GetAsync($"/api/contract-documents/{document.Id}/file");
        Assert.Equal(Pdf, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/contract-documents/{document.Id}")).StatusCode);
    }

    [Fact]
    public async Task Dispatcher_OnlyReadsContracts_CannotReachCourtFilesById()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");
        foreach (var kind in new[] { "Договор", "Решение суда" })
            (await lawyer.PostAsJsonAsync("/api/subscribers/3/documents", new AddDocumentRequest(kind, kind, Pdf))).EnsureSuccessStatusCode();
        var courtDocument = (await lawyer.GetFromJsonAsync<List<DocumentInfo>>("/api/subscribers/3/documents"))!.Single(d => d.Kind == "Решение суда");
        var dispatcher = await app.LoginAsync("orlova");
        var response = await dispatcher.GetAsync("/api/subscribers/3/contract-documents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = Assert.Single((await response.Content.ReadFromJsonAsync<List<DocumentInfo>>())!);
        Assert.Equal("Договор", document.Kind);
        Assert.Equal(HttpStatusCode.OK, (await dispatcher.GetAsync($"/api/contract-documents/{document.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dispatcher.GetAsync($"/api/contract-documents/{courtDocument.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.GetAsync($"/api/documents/{courtDocument.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.DeleteAsync($"/api/contract-documents/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.PostAsJsonAsync("/api/subscribers/3/contract-documents", new AddDocumentRequest("Договор", "Договор", Pdf))).StatusCode);
    }

    [Fact]
    public async Task ContractEditor_CannotUploadOrDeleteCourtDocumentThroughContractRoutes()
    {
        await using var app = new AversApp();
        var lawyer = await app.LoginAsync("lawyer");
        (await lawyer.PostAsJsonAsync("/api/subscribers/3/documents", new AddDocumentRequest("Решение суда", "Решение", Pdf))).EnsureSuccessStatusCode();
        var document = Assert.Single((await lawyer.GetFromJsonAsync<List<DocumentInfo>>("/api/subscribers/3/documents"))!);
        var head = await app.LoginAsync("orlov");
        Assert.Equal(HttpStatusCode.NotFound, (await head.DeleteAsync($"/api/contract-documents/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync("/api/subscribers/3/contract-documents", new AddDocumentRequest("Решение суда", "Решение", Pdf))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lawyer.GetAsync($"/api/documents/{document.Id}/file")).StatusCode);
    }

    [Fact]
    public async Task Reader_CannotReadContractDocuments()
    {
        await using var app = new AversApp();
        var reader = await app.LoginAsync("novikov");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/subscribers/3/contract-documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/contract-documents/1/file")).StatusCode);
    }
}
