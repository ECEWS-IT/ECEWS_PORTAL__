using System.Collections.Generic;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface IPortalService
{
	PortalViewModel GetPortalData(string? userEmail = null, bool isAdmin = false);

	IReadOnlyList<AppItem> GetAllApps();

	IReadOnlyList<AppItem> GetAppsForUser(string? userEmail, bool isAdmin);

	IReadOnlyList<AppItem> SearchApps(string? query, string? category, string? userEmail = null, bool isAdmin = false);
}
