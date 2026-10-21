using Avers.Domain;

namespace Avers.Data;

/// <summary>Новая квартира (абонент) в доме. Метод возвращает текст ошибки или null.</summary>
public static class Apartments
{
    /// <summary>
    /// Заводит абонента с договором и начислениями текущего года: с месяца договора (или с января,
    /// если договор старше) по декабрь по тарифу <paramref name="tariff"/>. Правка попадает в журнал изменений.
    /// </summary>
    public static string? Add(AversDbContext db, Subscriber draft, decimal tariff, DateOnly today, string who, DateTimeOffset at)
    {
        var apartment = draft.Apartment.Trim();
        var section = draft.Section.Trim();
        var account = draft.Account.Trim();
        var lastName = draft.LastName.Trim();
        if (apartment.Length is 0 or > 16 || section.Length > 8)
            return "Квартира — от 1 до 16 символов, секция — до 8.";
        if (account.Length is 0 or > 32)
            return "Лицевой счёт — от 1 до 32 символов.";
        if (lastName.Length is 0 or > 80 || draft.FirstName.Trim().Length > 80 || draft.Patronymic.Trim().Length > 80)
            return "Фамилия обязательна; фамилия, имя и отчество — до 80 символов.";
        if (draft.Phone.Trim().Length > 40 || draft.ContractNumber.Trim().Length > 40)
            return "Телефон и номер договора — до 40 символов.";
        if (draft.ContractDate is not { } signed || signed.Year > today.Year)
            return $"Дата договора обязательна и не позже {today.Year} года.";
        if (tariff <= 0 || decimal.Round(tariff, 2) != tariff)
            return "Тариф — больше нуля, не больше двух знаков после запятой.";
        var house = db.Houses.FirstOrDefault(h => h.Id == draft.HouseId);
        if (house is null)
            return "Дом не найден.";
        if (db.Subscribers.Any(s => s.Account == account))
            return $"Лицевой счёт {account} уже есть.";
        if (db.Subscribers.Any(s => s.HouseId == house.Id && s.Apartment == apartment && s.Section == section))
            return $"Квартира {apartment} в этом доме уже есть: новый договор оформите в ней.";

        var subscriber = new Subscriber
        {
            HouseId = house.Id,
            Apartment = apartment,
            Section = section,
            Account = account,
            LastName = lastName,
            FirstName = draft.FirstName.Trim(),
            Patronymic = draft.Patronymic.Trim(),
            Phone = draft.Phone.Trim(),
            ContractNumber = draft.ContractNumber.Trim(),
            ContractDate = signed
        };
        var from = signed.Year == today.Year ? signed.Month : 1;
        for (var month = from; month <= 12; month++)
            subscriber.Payments.Add(new YearPayment { Year = today.Year, Month = month, Tariff = tariff });
        db.Subscribers.Add(subscriber);
        ChangeLog.Add(db, at, who, "Новая квартира", ChangeLog.Account(subscriber), ChangeLog.Diff(
            ("Номер договора", null, subscriber.ContractNumber),
            ("Заключён", null, signed),
            ("Тариф", null, tariff)));
        db.SaveChanges();
        return null;
    }
}
