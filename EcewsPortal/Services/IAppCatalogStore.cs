using System.Collections.Generic;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface IAppCatalogStore
{
	IReadOnlyList<AppItem> GetCustomApps();

	IReadOnlyDictionary<int, AppItem> GetOverrides();

	IReadOnlySet<int> GetRemovedIds();

	AppItem AddApp(AppItem app);

	bool UpdateApp(
		int id,
		string name,
		string? url,
		string description,
		string? logoUrl = null,
		string? visibility = null,
		IEnumerable<string>? allowedEmails = null);

	bool DeleteApp(int id);
}
