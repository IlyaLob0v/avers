using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Avers.Server.Auth;

public static class AuthEndpoints
{
    public const string LoginRateLimit = "login";

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Вход");

        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting(LoginRateLimit);
        group.MapGet("/me", Me).RequireAuthorization();
    }

    /// <summary>Проверяет пароль и выдаёт токен. Ответ — <c>{ accessToken, expiresIn, refreshToken }</c>.</summary>
    private static async Task<Results<SignInHttpResult, UnauthorizedHttpResult>> Login(
        LoginRequest request, AversDbContext db, CancellationToken ct)
    {
        var login = request.Login.Trim();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login, ct);
        if (user is null || !Password.Verify(user, request.Password))
            return TypedResults.Unauthorized();

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(AversClaims.UserId, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(AversClaims.CanEditAccount, user.CanEditAccount ? "true" : "false")
        ], BearerTokenDefaults.AuthenticationScheme);

        return TypedResults.SignIn(new ClaimsPrincipal(identity), authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
    }

    private static async Task<Results<Ok<CurrentUserDto>, UnauthorizedHttpResult>> Me(
        ClaimsPrincipal principal, AversDbContext db, CancellationToken ct)
    {
        var login = principal.Identity?.Name;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login, ct);
        if (user is null)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new CurrentUserDto(
            user.Login,
            user.DisplayName,
            user.Role.ToString(),
            RoleCatalog.Title(user.Role),
            user.Department,
            user.CanEditAccount,
            Policies.CanRecordPayment(user.Role, user.CanEditAccount)));
    }
}
