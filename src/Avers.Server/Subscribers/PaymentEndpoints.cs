using System.Security.Claims;
using System.Text.Json;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Subscribers;

public static class PaymentEndpoints
{
    public static PaymentDto ToDto(YearPayment p) =>
        new(p.Id, p.Year, p.Month, p.Tariff, p.Amount, p.PaidAt, p.Source, p.Amount is not null, p.RecordedBy, p.PayerName, p.Revision);

    /// <summary>Вносит оплату за месяц (<see cref="Payments.Record"/>).</summary>
    public static async Task<Results<Ok<PaymentDto>, NotFound, ValidationProblem, ProblemHttpResult>> Record(
        int id, RecordPaymentRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Year is < 2000 or > 2100)
            errors["year"] = ["Год вне допустимого диапазона."];
        if (request.Month is < 1 or > 12)
            errors["month"] = ["Месяц — от 1 до 12."];
        if (request.OperationId is not null && request.Month is null)
            errors["month"] = ["Для повторяемой операции укажите месяц оплаты."];
        if (request.OperationId == Guid.Empty)
            errors["operationId"] = ["Укажите идентификатор операции."];
        if (request.Amount is < 0)
            errors["amount"] = ["Сумма не может быть отрицательной."];
        if (request.Amount > Payments.MaxAmount)
            errors["amount"] = ["Сумма — не больше 9 999 999 999,99 ₽."];
        if (request.Amount is not null && decimal.Round(request.Amount.Value, 2) != request.Amount.Value)
            errors["amount"] = ["Сумма — не больше двух знаков после запятой."];
        if (request.Source is { Length: > 40 })
            errors["source"] = ["Источник — не длиннее 40 символов."];
        if (request.PayerName is { Length: > 250 })
            errors["payerName"] = ["Плательщик — не длиннее 250 символов."];
        if (request.PaidAt is { } paidAt && (paidAt > time.Today() || paidAt.Year < 2000))
            errors["paidAt"] = ["Дата оплаты — не позже сегодняшней и не раньше 2000 года."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var userId = user.AppUserId();
        var requestJson = JsonSerializer.Serialize(request);
        async Task<PaymentOperation?> FindOperation() => request.OperationId is { } operationId
            ? await db.PaymentOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, ct)
            : null;
        Results<Ok<PaymentDto>, NotFound, ValidationProblem, ProblemHttpResult> Replay(PaymentOperation operation) =>
            operation.UserId == userId && operation.SubscriberId == id && operation.Request == requestJson
                ? TypedResults.Ok(JsonSerializer.Deserialize<PaymentDto>(operation.Receipt)!)
                : Conflict("Этот идентификатор уже использован для другой операции.");

        if (await FindOperation() is { } previous)
            return Replay(previous);

        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subscriber is null)
            return TypedResults.NotFound();

        var login = user.Identity?.Name;
        var recordedBy = await db.Users.AsNoTracking().Where(u => u.Login == login)
            .Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? login ?? "";
        try
        {
            var (target, error) = Payments.Record(db, subscriber, request.Year, request.Month, request.Amount,
                request.PaidAt ?? time.Today(), request.Source, request.PayerName, recordedBy, ChangeLog.Who(db, userId), time.GetUtcNow(),
                request.OperationId is { } operationId ? payment => db.PaymentOperations.Add(new PaymentOperation
                {
                    Id = operationId,
                    UserId = userId,
                    SubscriberId = id,
                    Request = requestJson,
                    Receipt = JsonSerializer.Serialize(ToDto(payment))
                }) : null);
            if (target is not null)
                return TypedResults.Ok(ToDto(target));
            return await FindOperation() is { } concurrent ? Replay(concurrent) : Conflict(error!);
        }
        catch (DbUpdateException) when (request.OperationId is not null)
        {
            // При повторе с изменённым месяцем уникальный ключ откатывает всю вторую транзакцию.
            if (await FindOperation() is { } concurrent)
                return Replay(concurrent);
            throw;
        }
    }

    private static ProblemHttpResult Conflict(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: "Оплата не внесена");

    public static Results<NoContent, ValidationProblem> Cancel(int id, int paymentId, long? revision, ClaimsPrincipal user, AversDbContext db, TimeProvider time)
    {
        if (revision is null or < 0)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["revision"] = ["Обновите карточку перед отменой оплаты."] });
        var error = PaymentCancellation.Cancel(db, id, paymentId, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow(), revision);
        return error is null
            ? TypedResults.NoContent()
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["payment"] = [error] });
    }

    /// <summary>Файл начислений ОЭК за месяц: оплаты в карточки и список расхождений (<see cref="OekImport"/>).</summary>
    public static Results<Ok<OekImportResult>, ValidationProblem> ImportOek(
        OekImportRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time)
    {
        if (request.Year is < 2000 or > 2100 || request.Month is < 1 or > 12)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Месяц файла — мм.гггг."] });
        if (request.Rows is not { Count: > 0 })
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["rows"] = ["В файле нет строк."] });
        if (request.Rows.Any(r => r is null))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["rows"] = ["В файле есть пустая строка."] });
        return TypedResults.Ok(OekImport.Apply(db, request, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow()));
    }
}
