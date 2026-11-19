using Avers.Contracts;
using Avers.Data;
using Avers.Domain;

namespace Avers.Desktop.Data;

/// <summary>
/// Откуда программа берёт данные: своя учебная база (<see cref="LocalStore"/>) или общая база
/// через сервер Аверса (<see cref="ServerStore"/>). Отказ — <see cref="StoreException"/> с текстом для сотрудника.
/// </summary>
public interface IAversStore
{
    /// <summary>Общая база на сервере: логин вводят руками, списка пользователей нет.</summary>
    bool IsShared { get; }

    /// <summary>Подпись на экране входа: где лежат данные.</summary>
    string Title { get; }

    DateOnly Today { get; }

    IReadOnlyList<AppUser> LoginUsers();

    /// <summary>Офлайн-карта на сервере Аверса (страница <c>/m/</c>), открытая на доме; null — карты нет (учебная база).</summary>
    Uri? MapAddress(int? houseId);

    Task<AppUser> SignInAsync(string login, string password);

    void SignOut();

    Task<AversSnapshot> LoadAsync(AppUser user);

    /// <summary>Абоненты дома с оплатами, делами и квитанциями, связанные с этим домом.</summary>
    Task<List<Subscriber>> LoadHouseAsync(House house);

    Task<List<SubscriberMatch>> FindSubscribersAsync(SearchMode mode, string query, string street, string house, string apartment, string section);

    /// <summary>Единственный найденный абонент или точное совпадение лицевого счёта.</summary>
    async Task<(int Id, int HouseId)?> FindSubscriberAsync(SearchMode mode, string query, string street, string house, string apartment)
    {
        var matches = await FindSubscribersAsync(mode, query, street, house, apartment, "");
        var exactAccount = mode == SearchMode.Account
            ? matches.FirstOrDefault(s => s.Account.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase)) : null;
        if (exactAccount is { } exact)
            return (exact.Id, exact.HouseId);
        return matches.Count switch
        {
            0 => null,
            1 => (matches[0].Id, matches[0].HouseId),
            _ => throw new StoreException("Найдено несколько абонентов. Уточните поиск или введите полный лицевой счёт.")
        };
    }

    Task<Debts> LoadDebtsAsync();

    Task<AccountingReport> LoadAccountingAsync(int year, int month);

    /// <summary>Оплата первого неоплаченного месяца года; сумма null — по тарифу месяца.</summary>
    Task<PaymentDto> RecordPaymentAsync(int subscriberId, RecordPaymentRequest request, AppUser user);

    Task SaveContractAsync(int subscriberId, UpdateContractRequest request);

    /// <summary>Судебное дело должника; Id = 0 — новое.</summary>
    Task SaveCourtCaseAsync(int subscriberId, CourtCase draft);

    Task CancelPaymentAsync(int subscriberId, int paymentId);

    Task<OekImportResult> ImportOekAsync(OekImportRequest request);

    /// <summary>Новая квартира: абонент с договором и тариф начислений.</summary>
    Task AddApartmentAsync(Subscriber draft, decimal tariff);

    Task SetPaymentWarningMonthsAsync(int months);

    Task SetPaymentConfirmFromAsync(decimal amount);

    /// <summary>Новый тариф с месяца; возвращает число изменённых месяцев.</summary>
    Task<int> ChangeTariffAsync(TariffChangeRequest request);

    /// <summary>Открывает начисления на год и возвращает число абонентов.</summary>
    Task<int> OpenYearAsync(int year);

    Task SaveHousePassportAsync(int houseId, HousePassportRequest request);

    /// <summary>Картинка схемы СКПТ; null — файл не загружен.</summary>
    Task<byte[]?> LoadSchemeFileAsync(int schemeId);

    Task AddSchemeAsync(int houseId, AddSchemeRequest request);

    Task ReplaceSchemeFileAsync(int schemeId, byte[] content);

    Task RenameSchemeAsync(int schemeId, string name);

    Task DeleteSchemeAsync(int schemeId);

    Task<List<DocumentInfo>> LoadDocumentsAsync(int subscriberId);

    /// <summary>Файл скана; null — скан удалён.</summary>
    Task<byte[]?> LoadDocumentFileAsync(int documentId);

    Task AddDocumentAsync(int subscriberId, AddDocumentRequest request);

    Task DeleteDocumentAsync(int documentId);

    /// <summary>Панель подъезда; Id = 0 — новая.</summary>
    Task SaveIntercomPanelAsync(int houseId, IntercomPanel draft);

    Task AddIntercomTicketAsync(int houseId, IntercomTicket draft);

    Task CloseIntercomTicketAsync(int ticketId);

    /// <summary>Словарь: черновик с Id = 0 — новая запись.</summary>
    Task SaveStreetAsync(Street draft);

    Task DeleteStreetAsync(int id);

    Task SaveHouseAsync(House draft);

    Task DeleteHouseAsync(int id);

    Task SaveOrganizationAsync(Organization draft);

    Task DeleteOrganizationAsync(int id);

    Task SaveMaterialAsync(Material draft);

    Task DeleteMaterialAsync(int id);

    Task SaveMechanicAsync(Mechanic draft);

    Task DeleteMechanicAsync(int id);

    /// <summary>
    /// Заявки по отбору, свежие сверху, не больше <see cref="TicketQueries.MaxResults"/>. У абонента только ФИО и лицевой счёт;
    /// дом связывает вызывающий по снимку.
    /// </summary>
    Task<List<WorkTicket>> LoadTicketsAsync(TicketFilter filter);

    /// <summary>Заявки квартиры абонента: свои и принятые по адресу квартиры.</summary>
    Task<List<WorkTicket>> LoadApartmentTicketsAsync(int subscriberId);

    /// <summary>Сводка за год по месяцам с отбором по дому, квартире и виду — считает база.</summary>
    Task<IReadOnlyList<TicketMonthStat>> LoadTicketMonthsAsync(int year, TicketFilter filter);

    /// <summary>Годы от первой заявки до текущего, новые сверху.</summary>
    Task<IReadOnlyList<int>> LoadTicketYearsAsync();

    /// <summary>Принято и выполнено по домам за период.</summary>
    Task<List<TicketHouseStat>> LoadTicketsByHouseAsync(DateOnly from, DateOnly to);

    /// <summary>Квартира открытой заявки: ФИО, лицевой счёт, договор, последняя оплата, примечание. null — заявка без абонента.</summary>
    Task<Subscriber?> LoadTicketApartmentAsync(int ticketId);

    /// <summary>Принимает заявку и возвращает её номер.</summary>
    Task<int> AcceptTicketAsync(CreateTicketRequest request, AppUser user);

    /// <summary>Записывает заявку и возвращает её такой, как сохранила база.</summary>
    Task<WorkTicket> SaveTicketAsync(int ticketId, string fault, string mechanic, string workDone, TicketStatus status, DateOnly? doneDate);

    Task MarkPprAsync(int houseId, int year, int month, PprStatus status);

    /// <summary>Переносит план ППР года на следующий и возвращает число работ.</summary>
    Task<int> CopyPprAsync(int year);

    /// <summary>Все сотрудники, отключённые в конце. Только для администратора.</summary>
    Task<IReadOnlyList<AppUser>> LoadUsersAsync();

    /// <summary>Сохраняет сотрудника (Id = 0 — новый); пустой пароль у существующего не меняется.</summary>
    Task SaveUserAsync(AppUser draft, string password, AppUser admin);

    /// <summary>Последние записи журнала изменений, новые сверху. Только для администратора.</summary>
    Task<IReadOnlyList<ChangeLogEntry>> LoadChangeLogAsync();

    /// <summary>Последние записи журнала просмотров персональных данных, новые сверху. Только для администратора.</summary>
    Task<IReadOnlyList<AccessLogEntry>> LoadAccessLogAsync();
}

public sealed class StoreException(string message, bool outcomeUnknown = false) : Exception(message)
{
    public bool OutcomeUnknown { get; } = outcomeUnknown;
}

public sealed record SubscriberMatch(int Id, int HouseId, string Account, string FullName, string Address, string Apartment, string Section)
{
    public string Label => $"{FullName} · {Address}{(Section.Length == 0 ? "" : $", секц. {Section}")}, кв. {Apartment} · л/с {Account}";
}
