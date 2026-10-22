using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Паспорт дома на экране «Антенны»: оборудование и ключи, с записью в журнал изменений.</summary>
public static class HousePassport
{
    /// <summary>Переносит поля паспорта из <paramref name="draft"/>; false — дом не найден.</summary>
    public static bool Save(AversDbContext db, int houseId, House draft, string who, DateTimeOffset at)
    {
        var house = db.Houses.Include(h => h.Street).FirstOrDefault(h => h.Id == houseId);
        if (house is null)
            return false;

        var antennas = draft.Antennas.Trim();
        var aerialCables = draft.AerialCables.Trim();
        var amplifiers = draft.Amplifiers.Trim();
        var powerSupplies = draft.PowerSupplies.Trim();
        var techFloorKey = draft.TechFloorKey.Trim();
        var otherNotes = draft.OtherNotes.Trim();
        ChangeLog.Add(db, at, who, "Паспорт дома", house.AddressLine, ChangeLog.Diff(
            ("Антенны, подъезды", house.Antennas, antennas),
            ("Воздушные линии", house.AerialCables, aerialCables),
            ("Усилители", house.Amplifiers, amplifiers),
            ("Блоки питания", house.PowerSupplies, powerSupplies),
            ("Ключ техэтажа", house.TechFloorKey, techFloorKey),
            ("Прочее", house.OtherNotes, otherNotes)));
        house.Antennas = antennas;
        house.AerialCables = aerialCables;
        house.Amplifiers = amplifiers;
        house.PowerSupplies = powerSupplies;
        house.TechFloorKey = techFloorKey;
        house.OtherNotes = otherNotes;
        db.SaveChanges();
        return true;
    }
}
