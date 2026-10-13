using System;
using System.Collections.Generic;

namespace EcewsPortal.Services;

public sealed class PortalAdminRecord
{
	public string Email { get; set; } = string.Empty;

	public HashSet<string> Pages { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
