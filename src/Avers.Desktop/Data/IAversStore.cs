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

    Task<AppUser> SignInAsync(string login, string password);

    void SignOut();

    Task<AversSnapshot> LoadAsync(AppUser user);

    Task<PaymentDto> RecordPaymentAsync(int subscriberId, int year, AppUser user);

    Task SaveContractAsync(int subscriberId, UpdateContractRequest request);

    Task SetPaymentWarningMonthsAsync(int months);

    Task SaveHousePassportAsync(int houseId, HousePassportRequest request);

    /// <summary>Картинка схемы СКПТ; null — файл не загружен.</summary>
    Task<byte[]?> LoadSchemeFileAsync(int schemeId);

    Task AddSchemeAsync(int houseId, AddSchemeRequest request);

    Task ReplaceSchemeFileAsync(int schemeId, byte[] content);

    /// <summary>Принимает заявку и возвращает её номер.</summary>
    Task<int> AcceptTicketAsync(CreateTicketRequest request, AppUser user);

    Task SaveTicketAsync(int ticketId, string fault, string mechanic, string workDone);

    Task MarkPprAsync(int houseId, int year, int month, PprStatus status);

    /// <summary>Переносит план ППР года на следующий и возвращает число работ.</summary>
    Task<int> CopyPprAsync(int year);

    /// <summary>Все сотрудники, отключённые в конце. Только для администратора.</summary>
    Task<IReadOnlyList<AppUser>> LoadUsersAsync();

    /// <summary>Сохраняет сотрудника (Id = 0 — новый); пустой пароль у существующего не меняется.</summary>
    Task SaveUserAsync(AppUser draft, string password, AppUser admin);

    /// <summary>Последние записи журнала изменений, новые сверху. Только для администратора.</summary>
    Task<IReadOnlyList<ChangeLogEntry>> LoadChangeLogAsync();
}

public sealed class StoreException(string message) : Exception(message);
