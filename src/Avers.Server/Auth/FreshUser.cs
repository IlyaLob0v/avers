using System.Security.Claims;
using Avers.Data;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Auth;

/// <summary>
/// Роль и права берутся из базы на каждом запросе, а не из токена: отключение сотрудника и смена роли
/// действуют сразу, а не после окончания сеанса (до 12 часов). Отключённому — 401, как при истёкшем сеансе.
/// </summary>
public static class FreshUser
{
    public static void UseFreshUser(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        var id = http.User.AppUserId();
        if (id != 0)
        {
            var db = http.RequestServices.GetRequiredService<AversDbContext>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, http.RequestAborted);
            if (user is null || user.IsDisabled)
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            http.User = new ClaimsPrincipal(new ClaimsIdentity(AversClaims.For(user), http.User.Identity!.AuthenticationType));
        }
        await next(http);
    });
}
