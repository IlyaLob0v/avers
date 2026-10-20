using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Домофоны: панели подъездов (с записью в журнал изменений) и заявки по домофону. Методы возвращают текст ошибки или null.</summary>
public static class Intercoms
{
    /// <summary>Сохраняет панель подъезда; <paramref name="draft"/> с Id = 0 — новая панель дома <paramref name="houseId"/>.</summary>
    public static string? SavePanel(AversDbContext db, int houseId, IntercomPanel draft, string who, DateTimeOffset at)
    {
        if (draft.Entrance is < 1 or > 99)
            return "Подъезд — число от 1 до 99.";
        if (new[] { draft.PanelType, draft.SwitchBlock, draft.Keys, draft.Power }.Any(f => f.Trim().Length > 200))
            return "Поле панели — не длиннее 200 символов.";
        var house = db.Houses.Include(h => h.Street).FirstOrDefault(h => h.Id == houseId);
        if (house is null)
            return "Дом не найден.";
        if (db.IntercomPanels.Any(p => p.HouseId == houseId && p.Entrance == draft.Entrance && p.Id != draft.Id))
            return $"Панель подъезда {draft.Entrance} уже есть.";

        var isNew = draft.Id == 0;
        var panel = isNew ? new IntercomPanel { HouseId = houseId } : db.IntercomPanels.FirstOrDefault(p => p.Id == draft.Id && p.HouseId == houseId);
        if (panel is null)
            return "Панель не найдена.";
        var changes = ChangeLog.Diff(
            ("Подъезд", isNew ? null : panel.Entrance, draft.Entrance),
            ("Тип панели", panel.PanelType, draft.PanelType),
            ("Блок коммутации", panel.SwitchBlock, draft.SwitchBlock),
            ("Ключи / ключницы", panel.Keys, draft.Keys),
            ("Питание", panel.Power, draft.Power));
        if (isNew)
            db.IntercomPanels.Add(panel);
        panel.Entrance = draft.Entrance;
        panel.PanelType = draft.PanelType.Trim();
        panel.SwitchBlock = draft.SwitchBlock.Trim();
        panel.Keys = draft.Keys.Trim();
        panel.Power = draft.Power.Trim();
        ChangeLog.Add(db, at, who, isNew ? "Домофон: новая панель" : "Домофон: панель", house.AddressLine, changes);
        db.SaveChanges();
        return null;
    }

    public static string? AddTicket(AversDbContext db, int houseId, IntercomTicket draft, DateOnly today)
    {
        var fault = draft.Fault.Trim();
        if (fault.Length is 0 or > 500)
            return "Неисправность — от 1 до 500 символов.";
        if (draft.Entrance is < 1 or > 99)
            return "Подъезд — число от 1 до 99.";
        if (!db.Houses.Any(h => h.Id == houseId))
            return "Дом не найден.";

        db.IntercomTickets.Add(new IntercomTicket
        {
            HouseId = houseId,
            Date = today,
            Entrance = draft.Entrance,
            Fault = fault,
            Mechanic = draft.Mechanic.Trim()
        });
        db.SaveChanges();
        return null;
    }

    public static string? MarkDone(AversDbContext db, int ticketId)
    {
        var ticket = db.IntercomTickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket is null)
            return "Заявка не найдена.";
        ticket.Done = true;
        db.SaveChanges();
        return null;
    }
}
