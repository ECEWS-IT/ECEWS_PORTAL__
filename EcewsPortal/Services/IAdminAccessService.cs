using System.Security.Claims;

namespace EcewsPortal.Services;

public interface IAdminAccessService
{
	string ResolveEmail(ClaimsPrincipal user);

	bool IsSuperAdmin(ClaimsPrincipal user);

	bool IsAdmin(ClaimsPrincipal user);

	bool CanAccess(ClaimsPrincipal user, string page);
}
