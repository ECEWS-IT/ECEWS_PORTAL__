using System.Collections.Generic;

namespace EcewsPortal.Services;

public interface IUserAppAssignmentStore
{
	IReadOnlyCollection<int> GetAssignedAppIds(string email);

	IReadOnlyDictionary<string, HashSet<int>> GetAllAssignments();

	void SetAssignments(string email, IEnumerable<int> appIds);

	bool HasAnyAssignments(string email);
}
