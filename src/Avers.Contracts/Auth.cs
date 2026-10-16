namespace Avers.Contracts;

/// <summary>Вход по логину и паролю. В ответ сервер отдаёт токен (поле <c>accessToken</c>).</summary>
public sealed record LoginRequest(string Login, string Password);

/// <summary>Кто вошёл и что ему можно.</summary>
public sealed record CurrentUserDto(
    int Id,
    string Login,
    string DisplayName,
    string Role,
    string RoleTitle,
    string Department,
    bool CanEditAccount,
    bool CanRecordPayment);
