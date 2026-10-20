using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;

namespace Avers.Desktop.Data;

/// <summary>Общая база через сервер Аверса. Права проверяет сервер; рабочая дата — сегодняшняя.</summary>
public sealed class ServerStore(HttpClient http) : IAversStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public bool IsShared => true;

    public string Title => $"сервер {http.BaseAddress?.Authority}";

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public IReadOnlyList<AppUser> LoginUsers() => [];

    public async Task<AppUser> SignInAsync(string login, string password)
    {
        SignOut();
        var response = await Send(() => http.PostAsJsonAsync("api/auth/login", new LoginRequest(login, password), Json));
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new StoreException("Неверный логин или пароль.");
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new StoreException("Слишком много попыток входа. Подождите минуту.");
        await EnsureSuccess(response);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var me = await Read<CurrentUserDto>(() => http.GetAsync("api/auth/me"));
        return new AppUser
        {
            Id = me.Id,
            Login = me.Login,
            DisplayName = me.DisplayName,
            Role = Enum.Parse<UserRole>(me.Role),
            Department = me.Department,
            CanEditAccount = me.CanEditAccount
        };
    }

    public void SignOut() => http.DefaultRequestHeaders.Authorization = null;

    public async Task<AversSnapshot> LoadAsync(AppUser user) =>
        (await Read<AversSnapshot>(() => http.GetAsync("api/desktop/snapshot"))).Link();

    public async Task<List<Subscriber>> LoadHouseAsync(House house) =>
        (await Read<HouseSubscribers>(() => http.GetAsync($"api/houses/{house.Id}/subscribers"))).Link(house);

    public async Task<(int Id, int HouseId)?> FindSubscriberAsync(SearchMode mode, string query, string street, string house, string apartment)
    {
        var url = $"api/subscribers?mode={mode}&q={Uri.EscapeDataString(query)}&street={Uri.EscapeDataString(street)}" +
                  $"&house={Uri.EscapeDataString(house)}&apartment={Uri.EscapeDataString(apartment)}";
        var found = await Read<List<SubscriberListItem>>(() => http.GetAsync(url));
        return found.FirstOrDefault() is { } s ? (s.Id, s.HouseId) : null;
    }

    public Task<Debts> LoadDebtsAsync() => Read<Debts>(() => http.GetAsync("api/debts"));

    public Task<AccountingReport> LoadAccountingAsync(int year, int month) =>
        Read<AccountingReport>(() => http.GetAsync($"api/reports/accounting?year={year}&month={month}"));

    public Task<PaymentDto> RecordPaymentAsync(int subscriberId, int year, AppUser user) =>
        Read<PaymentDto>(() => http.PostAsJsonAsync($"api/subscribers/{subscriberId}/payments", new RecordPaymentRequest(year), Json));

    public Task SaveContractAsync(int subscriberId, UpdateContractRequest request) =>
        Write(() => http.PutAsJsonAsync($"api/subscribers/{subscriberId}/contract", request, Json));

    public Task SetPaymentWarningMonthsAsync(int months) =>
        Write(() => http.PutAsJsonAsync("api/settings/payment-warning", new PaymentWarningSettings(months), Json));

    public Task SaveHousePassportAsync(int houseId, HousePassportRequest request) =>
        Write(() => http.PutAsJsonAsync($"api/houses/{houseId}/passport", request, Json));

    public async Task<byte[]?> LoadSchemeFileAsync(int schemeId)
    {
        using var response = await Send(() => http.GetAsync($"api/schemes/{schemeId}/file"));
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccess(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public Task AddSchemeAsync(int houseId, AddSchemeRequest request) =>
        Write(() => http.PostAsJsonAsync($"api/houses/{houseId}/schemes", request, Json));

    public Task ReplaceSchemeFileAsync(int schemeId, byte[] content) =>
        Write(() => http.PutAsJsonAsync($"api/schemes/{schemeId}/file", new SchemeFileRequest(content), Json));

    public Task RenameSchemeAsync(int schemeId, string name) =>
        Write(() => http.PutAsJsonAsync($"api/schemes/{schemeId}/name", new RenameSchemeRequest(name), Json));

    public Task DeleteSchemeAsync(int schemeId) =>
        Write(() => http.DeleteAsync($"api/schemes/{schemeId}"));

    public Task SaveStreetAsync(Street draft) =>
        Upsert("streets", draft.Id, new StreetRequest(draft.Name));

    public Task DeleteStreetAsync(int id) => Write(() => http.DeleteAsync($"api/streets/{id}"));

    public Task SaveHouseAsync(House draft) =>
        Upsert("houses", draft.Id, new HouseRequest(draft.StreetId, draft.Number, draft.PzhreuIndex, draft.PzhreuAccount,
            draft.Floors, draft.Entrances, draft.Risers, draft.Apartments, draft.Zone, draft.OrganizationId, draft.Serviced));

    public Task DeleteHouseAsync(int id) => Write(() => http.DeleteAsync($"api/houses/{id}"));

    public Task SaveMaterialAsync(Material draft) =>
        Upsert("materials", draft.Id, new MaterialRequest(draft.Name, draft.Unit));

    public Task DeleteMaterialAsync(int id) => Write(() => http.DeleteAsync($"api/materials/{id}"));

    public Task SaveMechanicAsync(Mechanic draft) =>
        Upsert("mechanics", draft.Id, new MechanicRequest(draft.FullName, draft.Position, draft.Phone, draft.Address));

    public Task DeleteMechanicAsync(int id) => Write(() => http.DeleteAsync($"api/mechanics/{id}"));

    /// <summary>Id = 0 — новая запись (POST), иначе правка (PUT).</summary>
    private Task Upsert<T>(string path, int id, T request) => Write(() => id == 0
        ? http.PostAsJsonAsync($"api/{path}", request, Json)
        : http.PutAsJsonAsync($"api/{path}/{id}", request, Json));

    public async Task<int> AcceptTicketAsync(CreateTicketRequest request, AppUser user) =>
        (await Read<TicketDto>(() => http.PostAsJsonAsync("api/tickets", request, Json))).Number;

    public Task SaveTicketAsync(int ticketId, string fault, string mechanic, string workDone) =>
        Write(() => http.PutAsJsonAsync($"api/tickets/{ticketId}", new UpdateTicketRequest(fault, workDone, mechanic), Json));

    public Task MarkPprAsync(int houseId, int year, int month, PprStatus status) =>
        Write(() => http.PutAsJsonAsync($"api/ppr/{houseId}/{year}/{month}", new PprMarkRequest(status.ToString()), Json));

    public async Task<int> CopyPprAsync(int year) =>
        (await Read<PprCopyResult>(() => http.PostAsync($"api/ppr/{year}/copy", null))).Copied;

    public async Task<IReadOnlyList<AppUser>> LoadUsersAsync() =>
        (await Read<List<UserDto>>(() => http.GetAsync("api/users"))).Select(u => new AppUser
        {
            Id = u.Id,
            Login = u.Login,
            DisplayName = u.DisplayName,
            Role = Enum.Parse<UserRole>(u.Role),
            Department = u.Department,
            CanEditAccount = u.CanEditAccount,
            IsDisabled = u.IsDisabled
        }).ToList();

    public Task SaveUserAsync(AppUser draft, string password, AppUser admin)
    {
        var request = new SaveUserRequest(draft.Login, draft.DisplayName, draft.Role.ToString(), draft.Department,
            draft.CanEditAccount, draft.IsDisabled, password);
        return Write(() => draft.Id == 0
            ? http.PostAsJsonAsync("api/users", request, Json)
            : http.PutAsJsonAsync($"api/users/{draft.Id}", request, Json));
    }

    public async Task<IReadOnlyList<ChangeLogEntry>> LoadChangeLogAsync() =>
        await Read<List<ChangeLogEntry>>(() => http.GetAsync("api/change-log"));

    private async Task<T> Read<T>(Func<Task<HttpResponseMessage>> request)
    {
        using var response = await Send(request);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task Write(Func<Task<HttpResponseMessage>> request)
    {
        using var response = await Send(request);
        await EnsureSuccess(response);
    }

    private async Task<HttpResponseMessage> Send(Func<Task<HttpResponseMessage>> request)
    {
        try
        {
            return await request();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new StoreException($"Нет связи с сервером {http.BaseAddress}. Проверьте сеть или спросите администратора.");
        }
    }

    /// <summary>Ответ сервера с ошибкой — в текст для сотрудника: подробность, первая ошибка поля или заголовок.</summary>
    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new StoreException("Сеанс истёк. Выйдите и войдите заново.");
            case HttpStatusCode.Forbidden:
                throw new StoreException("Нет прав на это действие.");
        }

        Problem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<Problem>(Json);
        }
        catch (JsonException)
        {
        }
        var message = problem?.Errors?.Values.SelectMany(x => x).FirstOrDefault()
                      ?? problem?.Detail ?? problem?.Title ?? $"Сервер ответил {(int)response.StatusCode}.";
        throw new StoreException(message);
    }

    private sealed record TokenResponse(string AccessToken);

    private sealed record Problem(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
