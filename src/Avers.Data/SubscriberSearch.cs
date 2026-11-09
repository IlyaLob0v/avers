using Avers.Domain;

namespace Avers.Data;

/// <summary>
/// Поиск абонента как на «Рабочем столе» оболочки: по адресу (улица с начала названия, дом, квартира),
/// лицевому счёту, внутреннему коду, ПЖРЭУ, фамилии, номеру договора или телефону. Выполняется в СУБД.
/// </summary>
public static class SubscriberSearch
{
    /// <summary>Меньше цифр — слишком много совпадений.</summary>
    public const int MinPhoneDigits = 5;

    /// <summary>Отбор абонентов; <c>Error</c> — что не так с запросом (поле и текст для сотрудника), тогда отбора нет.</summary>
    public static (IQueryable<Subscriber>? Query, string Field, string Error) Where(
        IQueryable<Subscriber> query, SearchMode mode, string? q, string? street, string? house, string? apartment)
    {
        var text = (q ?? "").Trim().ToLower();
        switch (mode)
        {
            case SearchMode.Address:
                var streetName = (street ?? "").Trim().ToLower();
                var houseNumber = (house ?? "").Trim().ToLower();
                if (streetName.Length == 0 || houseNumber.Length == 0)
                    return (null, "street", "Для поиска по адресу нужны улица и дом.");
                query = query.Where(s => s.House.Street.Name.ToLower().StartsWith(streetName) && s.House.Number.ToLower() == houseNumber);
                if (!string.IsNullOrWhiteSpace(apartment))
                {
                    var apt = apartment.Trim();
                    query = query.Where(s => s.Apartment == apt);
                }
                return (query, "", "");
            case SearchMode.Surname:
                if (text.Length == 0)
                    return (null, "q", "Введите фамилию.");
                // Короткую фамилию (Ли, Ким) ищем целиком, иначе по трём буквам нашлись бы сотни.
                return (text.Length < 4
                    ? query.Where(s => s.LastName.ToLower() == text)
                    : query.Where(s => s.LastName.ToLower().StartsWith(text)), "", "");
            case SearchMode.Phone:
                // Номер звонящего в любой записи: «+7 (983) 623-31-22», «8-983-623-3122» или последние цифры.
                // Код страны (+7 или 8) отбрасывается: сравниваются последние десять цифр.
                var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
                if (digits.Length < MinPhoneDigits)
                    return (null, "q", $"Введите не меньше {MinPhoneDigits} цифр телефона.");
                if (digits.Length > 10)
                    digits = digits[^10..];
                return (query.Where(s => s.Phone.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").Replace("+", "")
                    .Contains(digits)), "", "");
            default:
                if (text.Length == 0)
                    return (null, "q", "Введите, что искать.");
                return (mode switch
                {
                    SearchMode.Account => query.Where(s => s.Account.ToLower().Contains(text)),
                    SearchMode.InnerCode => query.Where(s => s.InnerCode.ToLower().Contains(text)),
                    SearchMode.Contract => query.Where(s => s.ContractNumber.ToLower().Contains(text)),
                    _ => query.Where(s => s.PzhreuIndex.ToLower().Contains(text) || s.PzhreuAccount.ToLower().Contains(text))
                }, "", "");
        }
    }
}
