using System.Security.Claims;
using Avers.Domain;
using Microsoft.AspNetCore.Authorization;
using static Avers.Domain.RoleCatalog;

namespace Avers.Server.Auth;

/// <summary>Утверждения в токене, которые выдаёт сервер при входе.</summary>
public static class AversClaims
{
    public const string UserId = "avers:uid";
    public const string CanEditAccount = "avers:can_edit_account";

    public static UserRole? Role(this ClaimsPrincipal user) =>
        Enum.TryParse<UserRole>(user.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public static int AppUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(UserId), System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0;

    public static bool CanEditAccountFlag(this ClaimsPrincipal user) =>
        user.FindFirstValue(CanEditAccount) == "true";
}

/// <summary>
/// Права проверяет сервер (CLAUDE.md, правило 6). Матрица модулей — <see cref="RoleCatalog"/>,
/// та же, что у оболочки.
/// </summary>
public static class Policies
{
    public const string SubscribersView = "subscribers.view";
    public const string PaymentsCreate = "payments.create";
    public const string TicketsView = "tickets.view";
    public const string TicketsCreate = "tickets.create";
    public const string SubscribersEdit = "subscribers.edit";
    public const string SettingsEdit = "settings.edit";
    public const string HousesView = "houses.view";
    public const string HousesEdit = "houses.edit";
    public const string SchemesView = "schemes.view";
    public const string SchemesEdit = "schemes.edit";
    public const string PhonebookEdit = "phonebook.edit";
    public const string UsersManage = "users.manage";
    public const string CourtCasesEdit = "court-cases.edit";

    public static void Add(AuthorizationOptions options)
    {
        options.AddPolicy(SubscribersView, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Abon, AccessLevel.View)));
        options.AddPolicy(PaymentsCreate, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Abon, AccessLevel.View), new RecordPaymentRequirement()));
        options.AddPolicy(TicketsView, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Disp, AccessLevel.View)));
        options.AddPolicy(TicketsCreate, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Disp, AccessLevel.Edit)));
        // Договор и примечание правит тот же круг, что и лицевой счёт (REQ-ADS-11).
        options.AddPolicy(SubscribersEdit, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Abon, AccessLevel.View), new RecordPaymentRequirement()));
        options.AddPolicy(HousesView, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Anten, AccessLevel.View)));
        options.AddPolicy(HousesEdit, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Anten, AccessLevel.Edit)));
        options.AddPolicy(SchemesView, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Scheme, AccessLevel.View)));
        options.AddPolicy(SchemesEdit, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Scheme, AccessLevel.Edit)));
        options.AddPolicy(PhonebookEdit, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Phonebook, AccessLevel.Edit)));
        // Общие настройки (порог жёлтой подсветки) — администратор и начальник АДС.
        options.AddPolicy(SettingsEdit, p => p.RequireAuthenticatedUser()
            .RequireRole(nameof(UserRole.Administrator), nameof(UserRole.AdsHead)));
        options.AddPolicy(CourtCasesEdit, p => p.RequireAuthenticatedUser()
            .AddRequirements(new ModuleAccessRequirement(AppModule.Legal, AccessLevel.Edit)));
        options.AddPolicy(UsersManage, p => p.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Administrator)));
    }

    /// <summary>
    /// Кто может вносить оплату — как в оболочке (<c>MainViewModel.RecordPayment</c>):
    /// администратор, начальник АДС или пользователь с правом правки лицевого счёта.
    /// </summary>
    public static bool CanRecordPayment(UserRole role, bool canEditAccount) =>
        canEditAccount || role is UserRole.Administrator or UserRole.AdsHead;

    public static bool HasAccess(UserRole role, AppModule module, AccessLevel minimum)
    {
        if (!Can(role, module, out var level))
            return false;
        return minimum switch
        {
            AccessLevel.Edit => level == AccessLevel.Edit,
            _ => true
        };
    }
}

public sealed record ModuleAccessRequirement(AppModule Module, AccessLevel Minimum) : IAuthorizationRequirement;

public sealed record RecordPaymentRequirement : IAuthorizationRequirement;

public sealed class AversAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var role = context.User.Role();
        if (role is null)
            return Task.CompletedTask;

        foreach (var requirement in context.PendingRequirements.ToList())
        {
            var ok = requirement switch
            {
                ModuleAccessRequirement m => Policies.HasAccess(role.Value, m.Module, m.Minimum),
                RecordPaymentRequirement => Policies.CanRecordPayment(role.Value, context.User.CanEditAccountFlag()),
                _ => (bool?)null
            };
            if (ok == true)
                context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
