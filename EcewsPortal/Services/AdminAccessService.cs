using System;
using System.Security.Claims;
using EcewsPortal.Models;
using Microsoft.Extensions.Options;

namespace EcewsPortal.Services;

public sealed class AdminAccessService(IOptions<AdminOptions> options, IAdminRoleStore roles) : IAdminAccessService
{
	public string ResolveEmail(ClaimsPrincipal user)
	{
		return user.FindFirstValue("preferred_username") ?? user.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? user.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn") ?? string.Empty;
	}

	public bool IsSuperAdmin(ClaimsPrincipal user)
	{
		string text = ResolveEmail(user);
		string superAdminEmail = AdminAccess.GetSuperAdminEmail(options.Value);
		return !string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(superAdminEmail) && string.Equals(text, superAdminEmail, StringComparison.OrdinalIgnoreCase);
	}

	public bool IsAdmin(ClaimsPrincipal user)
	{
		return IsSuperAdmin(user) || roles.Get(ResolveEmail(user)) != null;
	}

	public bool CanAccess(ClaimsPrincipal user, string page)
	{
		return IsSuperAdmin(user) || (roles.Get(ResolveEmail(user))?.Pages.Contains(page) ?? false);
	}
}
