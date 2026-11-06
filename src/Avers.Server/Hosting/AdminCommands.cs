using Avers.Data;
using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Hosting;

/// <summary>
/// Команды администратора без запуска веб-сервера: <c>Avers.Server set-password &lt;логин&gt;</c>.
/// Новый пароль читается из стандартного ввода, чтобы не оставаться в истории команд.
/// В пустой базе (первая установка) команда заводит этого сотрудника администратором.
/// </summary>
public static class AdminCommands
{
    public const int MinPasswordLength = UserAccounts.MinPasswordLength;

    public static bool TryRun(string[] args, out Func<IServiceProvider, Task<int>> command)
    {
        command = args switch
        {
            ["set-password", var login] => services => SetPasswordAsync(services, login, Console.In),
            ["demo-passwords"] => services => DemoPasswordsAsync(services, Console.Out),
            _ => null!
        };
        return command is not null;
    }

    /// <summary>
    /// Включённые сотрудники с учебным паролем <c>avers</c> (по одному логину в строке). Код 1 — такие есть.
    /// Им пользуется <c>avers-internet</c>: с учебным паролем из интернета вошёл бы кто угодно.
    /// </summary>
    public static async Task<int> DemoPasswordsAsync(IServiceProvider services, TextWriter output)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AversDbContext>();
        var users = await db.Users.AsNoTracking().Where(u => !u.IsDisabled).OrderBy(u => u.Login).ToListAsync();
        var weak = users.Where(u => Password.Verify(u, DemoSeeder.DemoPassword)).Select(u => u.Login).ToList();
        foreach (var login in weak)
            await output.WriteLineAsync(login);
        return weak.Count == 0 ? 0 : 1;
    }

    public static async Task<int> SetPasswordAsync(IServiceProvider services, string login, TextReader input)
    {
        var password = (await input.ReadLineAsync())?.TrimEnd('\r') ?? "";
        if (password.Length < MinPasswordLength)
        {
            await Console.Error.WriteLineAsync($"Пароль — не короче {MinPasswordLength} символов.");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AversDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login);
        if (user is null && !await db.Users.AnyAsync())
        {
            user = new AppUser { Login = login, DisplayName = "Администратор", Role = UserRole.Administrator, CanEditAccount = true };
            db.Users.Add(user);
        }
        if (user is null)
        {
            await Console.Error.WriteLineAsync($"Пользователя {login} нет. Есть: " +
                string.Join(", ", await db.Users.OrderBy(u => u.Login).Select(u => u.Login).ToListAsync()));
            return 1;
        }

        user.PasswordHash = Password.Hash(password);
        ChangeLog.Add(db, services.GetRequiredService<TimeProvider>().GetUtcNow(), "команда avers-password",
            "Изменение сотрудника", $"Сотрудник {login}", ["Пароль: изменён"]);
        await db.SaveChangesAsync();
        Console.WriteLine($"Пароль пользователя {login} изменён.");
        return 0;
    }
}
