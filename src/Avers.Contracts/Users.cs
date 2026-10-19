namespace Avers.Contracts;

/// <summary>Сотрудник в списке администратора. Role — имя из <c>UserRole</c>.</summary>
public sealed record UserDto(
    int Id,
    string Login,
    string DisplayName,
    string Role,
    string Department,
    bool CanEditAccount,
    bool IsDisabled);

/// <summary>
/// Новый сотрудник или правка существующего. Password: у нового обязателен,
/// у существующего пустой — пароль не меняется.
/// </summary>
public sealed record SaveUserRequest(
    string Login,
    string DisplayName,
    string Role,
    string Department,
    bool CanEditAccount,
    bool IsDisabled,
    string? Password);
