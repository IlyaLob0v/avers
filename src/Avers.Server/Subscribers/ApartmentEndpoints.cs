using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Avers.Server.Auth;
using Avers.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Subscribers;

/// <summary>Карточка квартиры: договор и примечание, порог жёлтой подсветки. Заявки по квартире — TicketEndpoints.</summary>
public static class ApartmentEndpoints
{
    public const int MaxNotesLength = 2000;

    public static async Task<Results<NoContent, NotFound, ValidationProblem>> UpdateContract(
        int id, UpdateContractRequest request, ClaimsPrincipal user, AversDbContext db, TimeProvider time, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ContractNumber is { Length: > 40 })
            errors["contractNumber"] = ["Номер договора — не длиннее 40 символов."];
        if (request.ContractHolder is { Length: > 250 })
            errors["contractHolder"] = ["ФИО — не длиннее 250 символов."];
        if (request.Notes is { Length: > MaxNotesLength })
            errors["notes"] = [$"Примечание — не длиннее {MaxNotesLength} символов."];
        if (request is { ContractDate: { } signed, ContractTerminated: { } ended } && ended < signed)
            errors["contractTerminated"] = ["Договор не может быть расторгнут раньше, чем заключён."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var s = await db.Subscribers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return TypedResults.NotFound();

        var changes = ChangeLog.Diff(
            ("Номер договора", s.ContractNumber, request.ContractNumber),
            ("Заключён", s.ContractDate, request.ContractDate),
            ("Расторгнут", s.ContractTerminated, request.ContractTerminated),
            ("С кем заключён", s.ContractHolder, request.ContractHolder),
            ("Примечание", s.Notes, request.Notes));
        s.ContractNumber = request.ContractNumber?.Trim() ?? "";
        s.ContractDate = request.ContractDate;
        s.ContractTerminated = request.ContractTerminated;
        s.ContractHolder = request.ContractHolder?.Trim() ?? "";
        s.Notes = request.Notes?.Trim() ?? "";
        ChangeLog.Add(db, time.GetUtcNow(), ChangeLog.Who(db, user.AppUserId()), "Договор и примечание", ChangeLog.Account(s), changes);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    public static Ok<PaymentWarningSettings> GetPaymentWarning(AversDbContext db) =>
        TypedResults.Ok(new PaymentWarningSettings(Settings.PaymentWarningMonths(db)));

    public static Results<Ok<PaymentWarningSettings>, ValidationProblem> SetPaymentWarning(
        PaymentWarningSettings request, AversDbContext db)
    {
        if (!PaymentAge.IsValidThreshold(request.Months))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["months"] = [$"Порог — от {PaymentAge.MinWarningMonths} до {PaymentAge.MaxWarningMonths} месяцев."]
            });
        Settings.SetPaymentWarningMonths(db, request.Months);
        return TypedResults.Ok(new PaymentWarningSettings(request.Months));
    }

    /// <summary>Начисления на текущий или следующий год (<see cref="Accruals.OpenYear"/>).</summary>
    public static Results<Ok<OpenYearResult>, ValidationProblem> OpenYear(
        int year, ClaimsPrincipal user, AversDbContext db, TimeProvider time)
    {
        var today = time.Today();
        if (year != today.Year && year != today.Year + 1)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["year"] = [$"Открыть можно {today.Year} или {today.Year + 1} год."]
            });
        return TypedResults.Ok(new OpenYearResult(
            Accruals.OpenYear(db, year, ChangeLog.Who(db, user.AppUserId()), time.GetUtcNow())));
    }
}
