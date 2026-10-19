using Avers.Data;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Hosting;

/// <summary>
/// Команды администратора без запуска веб-сервера: <c>Avers.Server set-password &lt;логин&gt;</c>.
/// Новый пароль читается из стандартного ввода, чтобы не оставаться в истории команд.
/// </summary>
public static class AdminCommands
{
    public const int MinPasswordLength = 6;

    public static bool TryRun(string[] args, out Func<IServiceProvider, Task<int>> command)
    {
        command = args switch
        {
            ["set-password", var login] => services => SetPasswordAsync(services, login, Console.In),
            _ => null!
        };
        return command is not null;
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
        if (user is null)
        {
            await Console.Error.WriteLineAsync($"Пользователя {login} нет. Есть: " +
                string.Join(", ", await db.Users.OrderBy(u => u.Login).Select(u => u.Login).ToListAsync()));
            return 1;
        }

        user.PasswordHash = Password.Hash(password);
        await db.SaveChangesAsync();
        Console.WriteLine($"Пароль пользователя {login} изменён.");
        return 0;
    }
}
