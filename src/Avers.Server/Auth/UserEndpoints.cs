using System.Security.Claims;
using Avers.Contracts;
using Avers.Data;
using Avers.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Avers.Server.Auth;

/// <summary>Сотрудники, их роли и пароли, журнал изменений — только администратор.</summary>
public static class UserEndpoints
{
    public static void MapUsers(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/users").WithTags("Сотрудники").RequireAuthorization(Policies.UsersManage);

        api.MapGet("/", (AversDbContext db) => UserAccounts.List(db).Select(ToDto));
        api.MapPost("/", (SaveUserRequest request, ClaimsPrincipal admin, AversDbContext db, TimeProvider time) => Save(0, request, admin, db, time));
        api.MapPut("/{id:int}", (int id, SaveUserRequest request, ClaimsPrincipal admin, AversDbContext db, TimeProvider time) => Save(id, request, admin, db, time));

        app.MapGet("/api/change-log", (AversDbContext db) => ChangeLog.Latest(db))
            .WithTags("Журнал изменений").RequireAuthorization(Policies.UsersManage);
    }

    private static Results<NoContent, ValidationProblem> Save(int id, SaveUserRequest request, ClaimsPrincipal admin, AversDbContext db, TimeProvider time)
    {
        var error = Enum.TryParse<UserRole>(request.Role, out var role) && Enum.IsDefined(role)
            ? UserAccounts.Save(db, new AppUser
            {
                Id = id,
                Login = request.Login ?? "",
                DisplayName = request.DisplayName ?? "",
                Role = role,
                Department = request.Department ?? "",
                CanEditAccount = request.CanEditAccount,
                IsDisabled = request.IsDisabled
            }, request.Password ?? "", admin.AppUserId(), time.GetUtcNow())
            : "Выберите роль.";
        return error is null
            ? TypedResults.NoContent()
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["user"] = [error] });
    }

    private static UserDto ToDto(AppUser u) =>
        new(u.Id, u.Login, u.DisplayName, u.Role.ToString(), u.Department, u.CanEditAccount, u.IsDisabled);
}
