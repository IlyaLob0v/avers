using Avers.Domain;

namespace Avers.Data;

/// <summary>
/// Договор, примечание, льгота и тип клиента (у юрлица — реквизиты; у физлица они стираются). Расторжение — это отключение: неоплаченные месяцы после месяца расторжения
/// снимаются (месяц расторжения начисляется полностью, Q-18). Снятое расторжение возвращает месяцы — с месяца после
/// прежнего расторжения или с месяца нового договора, если он позже, по декабрь текущего года по последнему тарифу.
/// </summary>
public static class SubscriberContracts
{
    public static void Save(AversDbContext db, Subscriber s, Subscriber draft, DateOnly today, string who, DateTimeOffset at)
    {
        var legal = draft.IsLegalEntity;
        draft.Inn = legal ? draft.Inn.Trim() : "";
        draft.Kpp = legal ? draft.Kpp.Trim() : "";
        draft.Ogrn = legal ? draft.Ogrn.Trim() : "";
        draft.LegalAddress = legal ? draft.LegalAddress.Trim() : "";
        draft.Director = legal ? draft.Director.Trim() : "";
        var changes = ChangeLog.Diff(
            ("Номер договора", s.ContractNumber, draft.ContractNumber),
            ("Заключён", s.ContractDate, draft.ContractDate),
            ("Расторгнут", s.ContractTerminated, draft.ContractTerminated),
            ("С кем заключён", s.ContractHolder, draft.ContractHolder),
            ("Примечание", s.Notes, draft.Notes),
            ("Льгота", s.Benefit, draft.Benefit),
            ("Клиент", s.ClientKind, draft.ClientKind),
            ("ИНН", s.Inn, draft.Inn),
            ("КПП", s.Kpp, draft.Kpp),
            ("ОГРН", s.Ogrn, draft.Ogrn),
            ("Юридический адрес", s.LegalAddress, draft.LegalAddress),
            ("Руководитель", s.Director, draft.Director));
        var before = s.ContractTerminated;
        s.ContractNumber = draft.ContractNumber.Trim();
        s.ContractDate = draft.ContractDate;
        s.ContractTerminated = draft.ContractTerminated;
        s.ContractHolder = draft.ContractHolder.Trim();
        s.Notes = draft.Notes.Trim();
        s.Benefit = draft.Benefit;
        s.IsLegalEntity = legal;
        s.Inn = draft.Inn;
        s.Kpp = draft.Kpp;
        s.Ogrn = draft.Ogrn;
        s.LegalAddress = draft.LegalAddress;
        s.Director = draft.Director;

        var months = db.Payments.Where(p => p.SubscriberId == s.Id).ToList();
        var stop = s.ContractTerminated is { } end ? Index(end) : int.MaxValue;
        var removed = months.Where(p => p.Amount is null && Index(p) > stop).ToList();
        db.Payments.RemoveRange(removed);
        if (removed.Count > 0)
            changes.Add($"Снято неоплаченных месяцев: {removed.Count}");

        if (before is { } old && Index(old) < stop && months.Count > 0)
        {
            var from = Math.Max(Index(old) + 1, s.ContractDate is { } signed ? Index(signed) : 0);
            var to = Math.Min(stop, today.Year * 12 + 11);
            var tariff = months.OrderBy(Index).Last().Tariff;
            var added = 0;
            for (var i = from; i <= to; i++)
            {
                if (months.Any(p => Index(p) == i)) continue;
                db.Payments.Add(new YearPayment { SubscriberId = s.Id, Year = i / 12, Month = i % 12 + 1, Tariff = tariff });
                added++;
            }
            if (added > 0)
                changes.Add($"Возвращено месяцев начислений: {added}");
        }

        ChangeLog.Add(db, at, who, "Договор и примечание", ChangeLog.Account(s), changes);
        db.SaveChanges();
    }

    private static int Index(DateOnly d) => d.Year * 12 + d.Month - 1;

    private static int Index(YearPayment p) => p.Year * 12 + p.Month - 1;
}
