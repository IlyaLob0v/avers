using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Сотрудники: администратор заводит, правит, меняет пароль и отключает. Не удаляем —
/// за сотрудником остаются его оплаты и заявки.
/// </summary>
public static class UserAccounts
{
    public const int MinPasswordLength = 6;

    public static List<AppUser> List(AversDbContext db) =>
        db.Users.AsNoTracking().OrderBy(u => u.IsDisabled).ThenBy(u => u.Login).ToList();

    /// <summary>
    /// Сохраняет сотрудника: <paramref name="draft"/> с Id = 0 — новый. Пустой <paramref name="password"/>
    /// у существующего — пароль не меняется. <paramref name="adminId"/> — кто сохраняет: себя он не отключает
    /// и роль администратора с себя не снимает. Возвращает текст ошибки или null.
    /// </summary>
    public static string? Save(AversDbContext db, AppUser draft, string password, int adminId)
    {
        var login = draft.Login.Trim();
        var name = draft.DisplayName.Trim();
        var isNew = draft.Id == 0;
        if (login.Length is 0 or > 40)
            return "Логин — от 1 до 40 символов.";
        if (name.Length == 0)
            return "Укажите фамилию и инициалы.";
        if ((isNew || password.Length > 0) && password.Length < MinPasswordLength)
            return $"Пароль — не короче {MinPasswordLength} символов.";
        if (draft.Id == adminId && (draft.IsDisabled || draft.Role != UserRole.Administrator))
            return "Нельзя отключить себя или снять с себя роль администратора.";
        if (db.Users.Any(u => u.Login == login && u.Id != draft.Id))
            return $"Логин {login} уже занят.";

        var user = isNew ? new AppUser() : db.Users.FirstOrDefault(u => u.Id == draft.Id);
        if (user is null)
            return "Сотрудник не найден.";
        if (isNew)
            db.Users.Add(user);

        user.Login = login;
        user.DisplayName = name;
        user.Role = draft.Role;
        user.Department = draft.Department.Trim();
        user.CanEditAccount = draft.CanEditAccount;
        user.IsDisabled = draft.IsDisabled;
        if (password.Length > 0)
            user.PasswordHash = Password.Hash(password);
        db.SaveChanges();
        return null;
    }
}
