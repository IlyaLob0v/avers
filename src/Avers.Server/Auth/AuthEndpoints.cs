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
    private static async Task<Results<SignInHttpResult, UnauthorizedHttpResult, ProblemHttpResult>> Login(
        LoginRequest request, AversDbContext db, LoginFailures failures, CancellationToken ct)
    {
        var login = request.Login?.Trim() ?? "";
        if (login.Length == 0 || string.IsNullOrEmpty(request.Password))
            return TypedResults.Unauthorized();
        if (failures.IsLocked(login))
            return TypedResults.Problem("Слишком много неверных паролей. Подождите 15 минут.", statusCode: StatusCodes.Status429TooManyRequests);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login, ct);
        if (user is null)
            return TypedResults.Unauthorized();
        if (user.IsDisabled || !Password.Verify(user, request.Password))
        {
            failures.Add(login);
            return TypedResults.Unauthorized();
        }
        failures.Reset(login);
        if (Password.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = Password.Hash(request.Password);
            await db.SaveChangesAsync(ct);
        }

        var identity = new ClaimsIdentity(AversClaims.For(user), BearerTokenDefaults.AuthenticationScheme);

        return TypedResults.SignIn(new ClaimsPrincipal(identity), authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
    }

    private static async Task<Results<Ok<CurrentUserDto>, UnauthorizedHttpResult>> Me(
        ClaimsPrincipal principal, AversDbContext db, CancellationToken ct)
    {
        var login = principal.Identity?.Name;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login, ct);
        if (user is null || user.IsDisabled)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new CurrentUserDto(
            user.Id,
            user.Login,
            user.DisplayName,
            user.Role.ToString(),
            RoleCatalog.Title(user.Role),
            user.Department,
            user.CanEditAccount,
            Policies.CanRecordPayment(user.Role, user.CanEditAccount)));
    }
}
