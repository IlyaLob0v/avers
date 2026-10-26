using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Паспорт дома на экране «Антенны»: оборудование, ключи, неустранённая неисправность и обслуживающая организация, с записью в журнал изменений.</summary>
public static class HousePassport
{
    /// <summary>
    /// Переносит поля паспорта из <paramref name="draft"/>; false — дом не найден. Пустая неисправность снимает и причину, и план.
    /// <c>draft.Organization</c> — новые сведения об организации дома (они общие для всех её домов); null — не трогать.
    /// </summary>
    public static bool Save(AversDbContext db, int houseId, House draft, string who, DateTimeOffset at)
    {
        var house = db.Houses.Include(h => h.Street).Include(h => h.Organization).FirstOrDefault(h => h.Id == houseId);
        if (house is null)
            return false;

        var antennas = draft.Antennas.Trim();
        var aerialCables = draft.AerialCables.Trim();
        var amplifiers = draft.Amplifiers.Trim();
        var powerSupplies = draft.PowerSupplies.Trim();
        var techFloorKey = draft.TechFloorKey.Trim();
        var otherNotes = draft.OtherNotes.Trim();
        var fault = string.IsNullOrWhiteSpace(draft.OpenFault) ? null : draft.OpenFault.Trim();
        var faultReason = fault is null || string.IsNullOrWhiteSpace(draft.OpenFaultReason) ? null : draft.OpenFaultReason.Trim();
        var faultFix = fault is null ? null : draft.OpenFaultPlannedFix;
        ChangeLog.Add(db, at, who, "Паспорт дома", house.AddressLine, ChangeLog.Diff(
            ("Антенны, подъезды", house.Antennas, antennas),
            ("Воздушные линии", house.AerialCables, aerialCables),
            ("Усилители", house.Amplifiers, amplifiers),
            ("Блоки питания", house.PowerSupplies, powerSupplies),
            ("Ключ техэтажа", house.TechFloorKey, techFloorKey),
            ("Прочее", house.OtherNotes, otherNotes),
            ("Неисправность", house.OpenFault, fault),
            ("Причина", house.OpenFaultReason, faultReason),
            ("План устранения", house.OpenFaultPlannedFix, faultFix)));
        house.Antennas = antennas;
        house.AerialCables = aerialCables;
        house.Amplifiers = amplifiers;
        house.PowerSupplies = powerSupplies;
        house.TechFloorKey = techFloorKey;
        house.OtherNotes = otherNotes;
        house.OpenFault = fault;
        house.OpenFaultReason = faultReason;
        house.OpenFaultPlannedFix = faultFix;
        if (draft.Organization is { } o && house.Organization is { } org)
        {
            var next = (Name: o.Name.Trim(), Branch: o.Branch.Trim(), Address: o.Address.Trim(), Phones: o.Phones.Trim(),
                HeadName: o.HeadName.Trim(), EngineerName: o.EngineerName.Trim());
            ChangeLog.Add(db, at, who, "Обслуживающая организация", $"{org.Name} (из паспорта: {house.AddressLine})", ChangeLog.Diff(
                ("Организация", org.Name, next.Name),
                ("Филиал", org.Branch, next.Branch),
                ("Адрес филиала", org.Address, next.Address),
                ("Телефоны", org.Phones, next.Phones),
                ("Начальник", org.HeadName, next.HeadName),
                ("Инженер", org.EngineerName, next.EngineerName)));
            (org.Name, org.Branch, org.Address, org.Phones, org.HeadName, org.EngineerName) = next;
        }
        db.SaveChanges();
        return true;
    }
}
