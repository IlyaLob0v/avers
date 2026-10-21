using Avers.Domain;

namespace Avers.Data;

/// <summary>Судебные дела должника: в JACK их нет, юристы вводят заново. Метод возвращает текст ошибки или null.</summary>
public static class CourtCases
{
    /// <summary>Сохраняет дело абонента; черновик с Id = 0 — новое дело. Правка попадает в журнал изменений.</summary>
    public static string? Save(AversDbContext db, int subscriberId, CourtCase draft, string who, DateTimeOffset at)
    {
        if (draft.DelaAversId.Trim().Length > 40 || draft.OrderNumber.Trim().Length > 40)
            return "Номер дела и номер приказа — до 40 символов.";
        if (draft.Court.Trim().Length > 200 || draft.Status.Trim().Length > 200)
            return "Суд и состояние дела — до 200 символов.";
        if (draft.DelaAversId.Trim() == "" && draft.OrderNumber.Trim() == "")
            return "Укажите номер дела или номер судебного приказа.";
        if (draft.Amount < 0 || decimal.Round(draft.Amount, 2) != draft.Amount)
            return "Сумма — не меньше нуля, не больше двух знаков после запятой.";
        var subscriber = db.Subscribers.FirstOrDefault(s => s.Id == subscriberId);
        if (subscriber is null)
            return "Абонент не найден.";
        var item = draft.Id == 0 ? new CourtCase { SubscriberId = subscriberId } : db.CourtCases.FirstOrDefault(c => c.Id == draft.Id && c.SubscriberId == subscriberId);
        if (item is null)
            return "Дело не найдено.";

        var isNew = draft.Id == 0;
        ChangeLog.Add(db, at, who, isNew ? "Новое судебное дело" : "Судебное дело", ChangeLog.Account(subscriber), ChangeLog.Diff(
            ("Дело-Аверс", isNew ? null : item.DelaAversId, draft.DelaAversId),
            ("Приказ", isNew ? null : item.OrderNumber, draft.OrderNumber),
            ("Дата приказа", isNew ? null : item.OrderDate, draft.OrderDate),
            ("Суд", isNew ? null : item.Court, draft.Court),
            ("Сумма", isNew ? null : item.Amount, draft.Amount),
            ("Состояние", isNew ? null : item.Status, draft.Status)));
        item.DelaAversId = draft.DelaAversId.Trim();
        item.OrderNumber = draft.OrderNumber.Trim();
        item.OrderDate = draft.OrderDate;
        item.Court = draft.Court.Trim();
        item.Amount = draft.Amount;
        item.Status = draft.Status.Trim();
        if (isNew)
            db.CourtCases.Add(item);
        db.SaveChanges();
        return null;
    }
}
