using System.Security.Claims;
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
    public const string DefaultSource = "Касса";

    public static PaymentDto ToDto(YearPayment p) =>
        new(p.Id, p.Year, p.Month, p.Tariff, p.Amount, p.PaidAt, p.Source, p.Amount is not null, p.RecordedBy, p.PayerName);

    /// <summary>
    /// Вносит оплату за месяц, как кнопка «Внести оплату» в оболочке. Оплаченным месяц считается,
    /// когда сумма заполнена, в том числе 0 ₽ — так в текущем коде (вопрос к бухгалтерии открыт,
    /// docs/07-open-questions.md).
    /// </summary>
    public static async Task<Results<Ok<PaymentDto>, NotFound, ValidationProblem, ProblemHttpResult>> Record(
        int id, RecordPaymentRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Year is < 2000 or > 2100)
            errors["year"] = ["Год вне допустимого диапазона."];
        if (request.Month is < 1 or > 12)
            errors["month"] = ["Месяц — от 1 до 12."];
        if (request.Amount is < 0)
            errors["amount"] = ["Сумма не может быть отрицательной."];
        if (request.Amount is not null && decimal.Round(request.Amount.Value, 2) != request.Amount.Value)
            errors["amount"] = ["Сумма — не больше двух знаков после запятой."];
        if (request.Source is { Length: > 40 })
            errors["source"] = ["Источник — не длиннее 40 символов."];
        if (request.PayerName is { Length: > 250 })
            errors["payerName"] = ["Плательщик — не длиннее 250 символов."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subscriber is null)
            return TypedResults.NotFound();

        var months = db.Payments.Where(p => p.SubscriberId == id && p.Year == request.Year);
        var target = request.Month is { } month
            ? await months.FirstOrDefaultAsync(p => p.Month == month, ct)
            : await months.Where(p => p.Amount == null).OrderBy(p => p.Month).FirstOrDefaultAsync(ct);

        if (target is null)
            return request.Month is null
                ? Conflict($"Все месяцы {request.Year} уже оплачены.")
                : Conflict($"За {request.Month:00}.{request.Year} у абонента нет начисления.");
        if (target.Amount is not null)
            return Conflict($"Месяц {target.Month:00}.{target.Year} уже оплачен.");

        var amount = request.Amount ?? target.Tariff;
        var paidAt = request.PaidAt ?? time.Today();
        var source = string.IsNullOrWhiteSpace(request.Source) ? DefaultSource : request.Source.Trim();
        var payer = string.IsNullOrWhiteSpace(request.PayerName) ? null : request.PayerName.Trim();
        var login = user.Identity?.Name;
        var recordedBy = await db.Users.AsNoTracking().Where(u => u.Login == login)
            .Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? login;

        // Условие Amount == null в самом UPDATE: если два кассира внесут один месяц одновременно,
        // пройдёт только первый.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Payments
            .Where(p => p.Id == target.Id && p.Amount == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(p => p.Amount, amount)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.Source, source)
                .SetProperty(p => p.RecordedBy, recordedBy)
                .SetProperty(p => p.PayerName, payer), ct);
        if (updated == 0)
            return Conflict($"Месяц {target.Month:00}.{target.Year} уже оплачен.");

        if (subscriber.LastPaymentDate is null || subscriber.LastPaymentDate < paidAt)
            subscriber.LastPaymentDate = paidAt;
        ChangeLog.Add(db, time.GetUtcNow(), ChangeLog.Who(db, user.AppUserId()), $"Оплата за {target.Month:00}.{target.Year}",
            ChangeLog.Account(subscriber), ChangeLog.Diff(
                ("Сумма", target.Amount, amount),
                ("Дата оплаты", target.PaidAt, paidAt),
                ("Источник", target.Source, source),
                ("Плательщик", target.PayerName, payer)));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        target.Amount = amount;
        target.PaidAt = paidAt;
        target.Source = source;
        target.RecordedBy = recordedBy;
        target.PayerName = payer;
        return TypedResults.Ok(ToDto(target));
    }

    private static ProblemHttpResult Conflict(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: "Оплата не внесена");

    public static Results<NoContent, ValidationProblem> Cancel(int id, int paymentId, ClaimsPrincipal user, AversDbContext db, TimeProvider time)
    {
        var error = PaymentCancellation.Cancel(db, id, paymentId, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow());
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
        if (request.Rows.Count == 0)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["rows"] = ["В файле нет строк."] });
        return TypedResults.Ok(OekImport.Apply(db, request, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow()));
    }
}
