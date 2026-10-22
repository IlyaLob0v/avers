using Avers.Domain;

namespace Avers.Data;

/// <summary>Судебные дела должника: в JACK их нет, юристы вводят заново. Метод возвращает текст ошибки или null.</summary>
public static class CourtCases
{
    public const int MaxTextLength = 2000;

    /// <summary>Сохраняет дело абонента; черновик с Id = 0 — новое дело. Правка попадает в журнал изменений.</summary>
    public static string? Save(AversDbContext db, int subscriberId, CourtCase draft, string who, DateTimeOffset at)
    {
        if (draft.OrderNumber.Trim().Length > 40)
            return "Номер приказа — до 40 символов.";
        if (draft.Court.Trim().Length > 200 || draft.Status.Trim().Length > 200)
            return "Суд и состояние дела — до 200 символов.";
        if (draft.OrderNumber.Trim() == "")
            return "Укажите номер судебного приказа.";
        if (draft.WritSentTo.Trim().Length > 40)
            return "Куда направлен лист — до 40 символов.";
        if (draft.Receipts.Trim().Length > MaxTextLength || draft.Notes.Trim().Length > MaxTextLength)
            return $"Поступления и примечание — до {MaxTextLength} символов.";
        if (draft.Amount < 0 || decimal.Round(draft.Amount, 2) != draft.Amount || draft.StateDuty < 0 || decimal.Round(draft.StateDuty, 2) != draft.StateDuty)
            return "Сумма и госпошлина — не меньше нуля, не больше двух знаков после запятой.";
        var subscriber = db.Subscribers.FirstOrDefault(s => s.Id == subscriberId);
        if (subscriber is null)
            return "Абонент не найден.";
        var item = draft.Id == 0 ? new CourtCase { SubscriberId = subscriberId } : db.CourtCases.FirstOrDefault(c => c.Id == draft.Id && c.SubscriberId == subscriberId);
        if (item is null)
            return "Дело не найдено.";

        var isNew = draft.Id == 0;
        ChangeLog.Add(db, at, who, isNew ? "Новое судебное дело" : "Судебное дело", ChangeLog.Account(subscriber), ChangeLog.Diff(
            ("Приказ", isNew ? null : item.OrderNumber, draft.OrderNumber),
            ("Дата приказа", isNew ? null : item.OrderDate, draft.OrderDate),
            ("Суд", isNew ? null : item.Court, draft.Court),
            ("Сумма", isNew ? null : item.Amount, draft.Amount),
            ("Состояние", isNew ? null : item.Status, draft.Status),
            ("Госпошлина", isNew ? null : item.StateDuty, draft.StateDuty),
            ("Лист направлен", isNew ? null : item.WritSentTo, draft.WritSentTo),
            ("Дата направления", isNew ? null : item.WritSentDate, draft.WritSentDate),
            ("Поступления", isNew ? null : item.Receipts, draft.Receipts),
            ("Примечание", isNew ? null : item.Notes, draft.Notes)));
        item.OrderNumber = draft.OrderNumber.Trim();
        item.OrderDate = draft.OrderDate;
        item.Court = draft.Court.Trim();
        item.Amount = draft.Amount;
        item.Status = draft.Status.Trim();
        item.StateDuty = draft.StateDuty;
        item.WritSentTo = draft.WritSentTo.Trim();
        item.WritSentDate = draft.WritSentDate;
        item.Receipts = draft.Receipts.Trim();
        item.Notes = draft.Notes.Trim();
        if (isNew)
            db.CourtCases.Add(item);
        db.SaveChanges();
        return null;
    }
}
