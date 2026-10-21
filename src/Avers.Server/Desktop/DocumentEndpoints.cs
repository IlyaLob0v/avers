using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Desktop;

/// <summary>Сканы документов абонента: смотрит тот, кому открыт раздел юристов, загружает и удаляет — кто ведёт судебные дела.</summary>
public static class DocumentEndpoints
{
    public static void MapDocuments(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Сканы документов");

        api.MapGet("/subscribers/{id:int}/documents", (int id, AversDbContext db) => TypedResults.Ok(SubscriberDocuments.List(db, id)))
            .RequireAuthorization(Policies.LegalView);
        api.MapGet("/documents/{id:int}/file", File).RequireAuthorization(Policies.LegalView);
        api.MapPost("/subscribers/{id:int}/documents", Add).RequireAuthorization(Policies.CourtCasesEdit);
        api.MapDelete("/documents/{id:int}", Delete).RequireAuthorization(Policies.CourtCasesEdit);
    }

    private static Results<FileContentHttpResult, NotFound> File(int id, AversDbContext db) =>
        SubscriberDocuments.File(db, id) is { } doc
            ? TypedResults.File(doc.Content, SubscriberDocuments.ContentType(doc.Format))
            : TypedResults.NotFound();

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> Add(
        int id, AddDocumentRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (SubscriberDocuments.Check(request.Kind, request.Name, request.Content) is { } error)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["document"] = [error] });

        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subscriber is null)
            return TypedResults.NotFound();

        SubscriberDocuments.Add(db, subscriber, request.Kind, request.Name, request.Content, time.Today(), ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static Results<NoContent, NotFound> Delete(int id, ClaimsPrincipal user, AversDbContext db, TimeProvider time) =>
        SubscriberDocuments.Delete(db, id, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow())
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
