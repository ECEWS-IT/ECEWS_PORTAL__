using EcewsPortal.Models;
using Microsoft.AspNetCore.Authorization;

namespace EcewsPortal.Services;

public sealed record AdminPermissionRequirement(
    string? Page = null,
    bool SuperAdminOnly = false) : IAuthorizationRequirement;

public sealed class AdminPermissionHandler(IAdminAccessService access)
    : AuthorizationHandler<AdminPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPermissionRequirement requirement)
    {
        var authorized = requirement.SuperAdminOnly
            ? access.IsSuperAdmin(context.User)
            : requirement.Page is null
                ? access.IsAdmin(context.User)
                : access.CanAccess(context.User, requirement.Page);

        if (authorized)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public static class AdminPolicies
{
    public const string AnyAdmin = "AdminOnly";
    public const string SuperAdmin = "SuperAdminOnly";
    public const string Applications = "AdminApplications";
    public const string StaffAccess = "AdminStaffAccess";
    public const string ActiveUsers = "AdminActiveUsers";
    public const string Analytics = "AdminAnalytics";
}

public static class AdminAccess
{
    public const string PolicyName = AdminPolicies.AnyAdmin;

    public static string GetSuperAdminEmail(AdminOptions options) =>
        !string.IsNullOrWhiteSpace(options.SuperAdminEmail)
            ? options.SuperAdminEmail.Trim()
            : (options.AdminEmails ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e)
                        && !e.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Trim())
            .FirstOrDefault() ?? string.Empty;
}
