using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Загрузка файла начислений ОЭК за месяц. Квартира ищется по нашему лицевому счёту (поле LS), без него — по счёту ОЭК.
/// Оплата ОЭК ложится в этот месяц квартиры, как в JACK (PayForDate = месяц файла). Уже оплаченный месяц не трогается:
/// строка уходит в расхождения. Повторная загрузка того же файла ничего не меняет.
/// </summary>
public static class OekImport
{
    public const string Source = "ОЭК";

    public static OekImportResult Apply(AversDbContext db, OekImportRequest request, string who, DateTimeOffset at)
    {
        var subscribers = db.Subscribers.ToList();
        var byAccount = subscribers.ToDictionary(s => s.Account);
        var byOek = subscribers.Where(s => s.OekAccount != "").GroupBy(s => s.OekAccount).ToDictionary(g => g.Key, g => g.First());
        var months = db.Payments.Where(p => p.Year == request.Year && p.Month == request.Month).ToDictionary(p => p.SubscriberId);

        var mismatches = new List<OekMismatch>();
        int matched = 0, payments = 0, already = 0;
        var total = 0m;
        foreach (var row in request.Rows)
        {
            var s = row.Account != "" && byAccount.TryGetValue(row.Account, out var a) ? a
                : byOek.TryGetValue(row.OekAccount, out var o) ? o : null;
            void Mismatch(decimal? tariff, string problem) =>
                mismatches.Add(new OekMismatch(row.OekAccount, row.Account, row.Address, row.Apartment, row.Accrued, tariff, row.Paid, problem));
            if (s is null)
            {
                Mismatch(null, "нет в Аверсе");
                continue;
            }
            matched++;
            s.OekAccount = row.OekAccount;

            var month = months.GetValueOrDefault(s.Id);
            if (month is null)
            {
                if (row.Accrued != 0 || row.Paid != 0)
                    Mismatch(null, row.Paid > 0 ? "в Аверсе нет начисления за месяц, оплата не загружена" : "в Аверсе нет начисления за месяц");
                continue;
            }
            if (month.Tariff != row.Accrued)
                Mismatch(month.Tariff, "начисление расходится");
            if (row.Paid <= 0)
                continue;
            if (month.Source == Source)
            {
                already++;
                continue;
            }
            if (month.Amount is not null)
            {
                Mismatch(month.Tariff, $"месяц уже оплачен в Аверсе: {month.Amount:0.00} ({month.Source}), оплата ОЭК не загружена");
                continue;
            }
            var paidAt = row.PaidAt ?? new DateOnly(request.Year, request.Month, 1);
            month.Amount = row.Paid;
            month.PaidAt = paidAt;
            month.Source = Source;
            month.RecordedBy = "Файл ОЭК";
            if (s.LastPaymentDate is null || s.LastPaymentDate < paidAt)
                s.LastPaymentDate = paidAt;
            payments++;
            total += row.Paid;
        }

        ChangeLog.Add(db, at, who, "Загрузка файла ОЭК", $"за {request.Month:00}.{request.Year}",
            [$"Строк: {request.Rows.Count}, найдено квартир: {matched}", $"Загружено оплат: {payments} на {total:0.00}",
             $"Уже были загружены: {already}", $"Расхождений: {mismatches.Count}"]);
        db.SaveChanges();
        return new OekImportResult(request.Rows.Count, matched, payments, total, already, mismatches);
    }
}

/// <summary>Строка файла начислений ОЭК по услуге «Антенна» (nachMMYYYY_an): одна на лицевой счёт ОЭК.</summary>
public sealed record OekRow(
    string OekAccount,
    string Account,
    string Address,
    string Apartment,
    decimal Accrued,
    decimal Paid,
    DateOnly? PaidAt);

/// <summary>Файл ОЭК за месяц: оплаты ложатся в этот месяц лицевого счёта.</summary>
public sealed record OekImportRequest(int Year, int Month, List<OekRow> Rows);

/// <summary>Строка файла, которую надо разобрать вручную: квартиры нет, начисление расходится или оплата не загружена.</summary>
public sealed record OekMismatch(string OekAccount, string Account, string Address, string Apartment,
    decimal OekAccrued, decimal? AversTariff, decimal OekPaid, string Problem);

public sealed record OekImportResult(int Rows, int Matched, int Payments, decimal PaidTotal, int AlreadyLoaded, List<OekMismatch> Mismatches);
