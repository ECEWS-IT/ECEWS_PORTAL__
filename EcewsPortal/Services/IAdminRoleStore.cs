using System.Collections.Generic;

namespace EcewsPortal.Services;

public interface IAdminRoleStore
{
	IReadOnlyList<PortalAdminRecord> GetAll();

	PortalAdminRecord? Get(string email);

	void Set(string email, IEnumerable<string> pages);

	bool Remove(string email);
}
