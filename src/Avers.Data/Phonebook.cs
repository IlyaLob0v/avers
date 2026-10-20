using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Словарь: улицы, дома, материалы, радиомеханики. Черновик с Id = 0 — новая запись. Улицу и дом,
/// на которые что-то ссылается, не удаляем. Правка попадает в журнал изменений. Методы возвращают текст ошибки или null.
/// </summary>
public static class Phonebook
{
    public static string? SaveStreet(AversDbContext db, Street draft, string who, DateTimeOffset at)
    {
        var name = draft.Name.Trim();
        if (name.Length is 0 or > 120)
            return "Название улицы — от 1 до 120 символов.";
        if (db.Streets.Any(s => s.Name == name && s.Id != draft.Id))
            return $"Улица {name} уже есть.";

        var street = draft.Id == 0 ? new Street() : db.Streets.FirstOrDefault(s => s.Id == draft.Id);
        if (street is null)
            return "Улица не найдена.";
        if (draft.Id == 0)
        {
            db.Streets.Add(street);
            ChangeLog.Add(db, at, who, "Словарь: улица", name, ["Добавлена"]);
        }
        else
        {
            ChangeLog.Add(db, at, who, "Словарь: улица", name, ChangeLog.Diff(("Название", street.Name, name)));
        }
        street.Name = name;
        db.SaveChanges();
        return null;
    }

    public static string? DeleteStreet(AversDbContext db, int id, string who, DateTimeOffset at)
    {
        var street = db.Streets.FirstOrDefault(s => s.Id == id);
        if (street is null)
            return "Улица не найдена.";
        var houses = db.Houses.Count(h => h.StreetId == id);
        if (houses > 0)
            return $"На улице {street.Name} есть дома ({houses}). Сначала удалите их.";

        db.Streets.Remove(street);
        ChangeLog.Add(db, at, who, "Словарь: улица", street.Name, ["Удалена"]);
        db.SaveChanges();
        return null;
    }

    public static string? SaveHouse(AversDbContext db, House draft, string who, DateTimeOffset at)
    {
        var number = draft.Number.Trim();
        if (number.Length is 0 or > 20)
            return "Номер дома — от 1 до 20 символов.";
        if (draft.PzhreuIndex.Trim().Length > 16 || draft.PzhreuAccount.Trim().Length > 32 || draft.Zone.Trim().Length > 80)
            return "Индекс ПЖРЭУ — до 16 символов, счёт — до 32, зона — до 80.";
        if (new[] { draft.Floors, draft.Entrances, draft.Risers, draft.Apartments }.Any(n => n is < 0 or > 9999))
            return "Этажи, подъезды, стояки и квартиры — числа от 0 до 9999.";
        var street = db.Streets.FirstOrDefault(s => s.Id == draft.StreetId);
        if (street is null)
            return "Улица не найдена.";
        if (draft.OrganizationId is { } org && !db.Organizations.Any(o => o.Id == org))
            return "Обслуживающая организация не найдена.";
        if (db.Houses.Any(h => h.StreetId == draft.StreetId && h.Number == number && h.Id != draft.Id))
            return $"Дом {number} на улице {street.Name} уже есть.";

        var house = draft.Id == 0 ? new House() : db.Houses.Include(h => h.Organization).FirstOrDefault(h => h.Id == draft.Id);
        if (house is null)
            return "Дом не найден.";
        var organization = db.Organizations.AsNoTracking().FirstOrDefault(o => o.Id == draft.OrganizationId)?.Name;
        var isNew = draft.Id == 0;
        var changes = ChangeLog.Diff(
            ("Номер", isNew ? null : house.Number, number),
            ("Индекс ПЖРЭУ", house.PzhreuIndex, draft.PzhreuIndex),
            ("Счёт ПЖРЭУ", house.PzhreuAccount, draft.PzhreuAccount),
            ("Этажей", isNew ? null : house.Floors, draft.Floors),
            ("Подъездов", isNew ? null : house.Entrances, draft.Entrances),
            ("Стояков", isNew ? null : house.Risers, draft.Risers),
            ("Квартир", isNew ? null : house.Apartments, draft.Apartments),
            ("Зона", house.Zone, draft.Zone),
            ("Организация", house.Organization?.Name, organization),
            ("На обслуживании", isNew ? null : house.Serviced, draft.Serviced));
        if (isNew)
            db.Houses.Add(house);

        house.StreetId = draft.StreetId;
        house.Number = number;
        house.PzhreuIndex = draft.PzhreuIndex.Trim();
        house.PzhreuAccount = draft.PzhreuAccount.Trim();
        house.Floors = draft.Floors;
        house.Entrances = draft.Entrances;
        house.Risers = draft.Risers;
        house.Apartments = draft.Apartments;
        house.Zone = draft.Zone.Trim();
        house.OrganizationId = draft.OrganizationId;
        house.Serviced = draft.Serviced;
        ChangeLog.Add(db, at, who, isNew ? "Словарь: новый дом" : "Словарь: дом", $"ул. {street.Name}, д. {number}", changes);
        db.SaveChanges();
        return null;
    }

    /// <summary>Дом без абонентов, заявок, схем, ППР и домофонов удаляется; с ними — только снимается с обслуживания.</summary>
    public static string? DeleteHouse(AversDbContext db, int id, string who, DateTimeOffset at)
    {
        var house = db.Houses.Include(h => h.Street).FirstOrDefault(h => h.Id == id);
        if (house is null)
            return "Дом не найден.";
        var linked = new (string Name, int Count)[]
        {
            ("абоненты", db.Subscribers.Count(x => x.HouseId == id)),
            ("заявки", db.Tickets.Count(x => x.HouseId == id)),
            ("схемы СКПТ", db.HouseSchemes.Count(x => x.HouseId == id)),
            ("график ППР", db.PprEntries.Count(x => x.HouseId == id)),
            ("домофоны", db.IntercomPanels.Count(x => x.HouseId == id) + db.IntercomTickets.Count(x => x.HouseId == id))
        }.Where(x => x.Count > 0).Select(x => x.Name).ToList();
        if (linked.Count > 0)
            return $"У дома есть {string.Join(", ", linked)}: удалить нельзя. Снимите отметку «На обслуживании».";

        db.Houses.Remove(house);
        ChangeLog.Add(db, at, who, "Словарь: дом", house.AddressLine, ["Удалён"]);
        db.SaveChanges();
        return null;
    }

    public static string? SaveMaterial(AversDbContext db, Material draft, string who, DateTimeOffset at)
    {
        var name = draft.Name.Trim();
        var unit = draft.Unit.Trim();
        if (name.Length is 0 or > 200)
            return "Наименование материала — от 1 до 200 символов.";
        if (unit.Length is 0 or > 10)
            return "Единица измерения — от 1 до 10 символов.";

        var material = draft.Id == 0 ? new Material() : db.Materials.FirstOrDefault(m => m.Id == draft.Id);
        if (material is null)
            return "Материал не найден.";
        if (draft.Id == 0)
        {
            db.Materials.Add(material);
            ChangeLog.Add(db, at, who, "Словарь: материал", name, [$"Добавлен, ед. {unit}"]);
        }
        else
        {
            ChangeLog.Add(db, at, who, "Словарь: материал", name,
                ChangeLog.Diff(("Наименование", material.Name, name), ("Ед.", material.Unit, unit)));
        }
        material.Name = name;
        material.Unit = unit;
        db.SaveChanges();
        return null;
    }

    public static string? DeleteMaterial(AversDbContext db, int id, string who, DateTimeOffset at)
    {
        var material = db.Materials.FirstOrDefault(m => m.Id == id);
        if (material is null)
            return "Материал не найден.";
        db.Materials.Remove(material);
        ChangeLog.Add(db, at, who, "Словарь: материал", material.Name, ["Удалён"]);
        db.SaveChanges();
        return null;
    }

    public static string? SaveMechanic(AversDbContext db, Mechanic draft, string who, DateTimeOffset at)
    {
        var name = draft.FullName.Trim();
        if (name.Length is 0 or > 120)
            return "ФИО радиомеханика — от 1 до 120 символов.";
        if (draft.Position.Trim().Length > 120 || draft.Phone.Trim().Length > 120 || draft.Address.Trim().Length > 300)
            return "Должность и телефон — до 120 символов, адрес — до 300.";

        var mechanic = draft.Id == 0 ? new Mechanic() : db.Mechanics.FirstOrDefault(m => m.Id == draft.Id);
        if (mechanic is null)
            return "Радиомеханик не найден.";
        var isNew = draft.Id == 0;
        var changes = ChangeLog.Diff(
            ("ФИО", isNew ? null : mechanic.FullName, name),
            ("Должность", mechanic.Position, draft.Position),
            ("Телефон", mechanic.Phone, draft.Phone),
            ("Адрес", mechanic.Address, draft.Address));
        if (isNew)
            db.Mechanics.Add(mechanic);
        mechanic.FullName = name;
        mechanic.Position = draft.Position.Trim();
        mechanic.Phone = draft.Phone.Trim();
        mechanic.Address = draft.Address.Trim();
        ChangeLog.Add(db, at, who, isNew ? "Словарь: новый радиомеханик" : "Словарь: радиомеханик", name, changes);
        db.SaveChanges();
        return null;
    }

    /// <summary>В заявках механик записан текстом, поэтому старые заявки после удаления не меняются.</summary>
    public static string? DeleteMechanic(AversDbContext db, int id, string who, DateTimeOffset at)
    {
        var mechanic = db.Mechanics.FirstOrDefault(m => m.Id == id);
        if (mechanic is null)
            return "Радиомеханик не найден.";
        db.Mechanics.Remove(mechanic);
        ChangeLog.Add(db, at, who, "Словарь: радиомеханик", mechanic.FullName, ["Удалён"]);
        db.SaveChanges();
        return null;
    }
}
