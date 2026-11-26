using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Desktop;

/// <summary>Сканы договоров доступны из карточки абонента; судебные документы остаются в юридическом разделе.</summary>
public static class ContractDocumentEndpoints
{
    public static void MapContractDocuments(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Сканы договоров").RequireAuthorization(Policies.SubscribersView);
        api.MapGet("/subscribers/{id:int}/contract-documents", (int id, AversDbContext db) =>
            TypedResults.Ok(SubscriberDocuments.List(db, id, SubscriberDocuments.ContractKind)));
        api.MapGet("/contract-documents/{id:int}/file", File)
            .LogsView("Скан договора", AccessLogFilter.Document("id"));
        api.MapPost("/subscribers/{id:int}/contract-documents", Add);
        api.MapDelete("/contract-documents/{id:int}", Delete);
    }

    private static Results<FileContentHttpResult, NotFound> File(int id, AversDbContext db) =>
        SubscriberDocuments.File(db, id, SubscriberDocuments.ContractKind) is { } doc
            ? TypedResults.File(doc.Content, SubscriberDocuments.ContentType(doc.Format))
            : TypedResults.NotFound();

    private static async Task<bool> CanEdit(ClaimsPrincipal user, IAuthorizationService authorization) =>
        (await authorization.AuthorizeAsync(user, Policies.SubscribersEdit)).Succeeded ||
        (await authorization.AuthorizeAsync(user, Policies.CourtCasesEdit)).Succeeded;

    private static async Task<Results<NoContent, NotFound, ValidationProblem, ForbidHttpResult>> Add(
        int id, AddDocumentRequest request, ClaimsPrincipal user, IAuthorizationService authorization,
        AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!await CanEdit(user, authorization)) return TypedResults.Forbid();
        var error = request.Kind != SubscriberDocuments.ContractKind
            ? "В этом разделе можно добавлять только сканы договора."
            : SubscriberDocuments.Check(SubscriberDocuments.ContractKind, request.Name, request.Content);
        if (error is not null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["document"] = [error] });
        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subscriber is null) return TypedResults.NotFound();
        SubscriberDocuments.Add(db, subscriber, SubscriberDocuments.ContractKind, request.Name, request.Content,
            time.Today(), ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Delete(
        int id, ClaimsPrincipal user, IAuthorizationService authorization, AversDbContext db, TimeProvider time)
    {
        if (!await CanEdit(user, authorization)) return TypedResults.Forbid();
        return SubscriberDocuments.Delete(db, id, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow(), SubscriberDocuments.ContractKind)
            ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
