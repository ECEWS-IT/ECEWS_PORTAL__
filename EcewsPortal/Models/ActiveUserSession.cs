using System;
using System.Collections.Generic;

namespace EcewsPortal.Models;

public sealed class ActiveUserSession
{
	public string UserId { get; init; } = string.Empty;

	public string Email { get; init; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public DateTimeOffset SignedInAt { get; set; }

	public DateTimeOffset LastSeenAt { get; set; }

	public string? IpAddress { get; set; }

	public HashSet<string> IpAddresses { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public string? UserAgent { get; set; }

	public string DeviceLabel { get; set; } = "Unknown";

	public bool MultiIp { get; set; }

	public string ActivityStatus { get; set; } = "active";
}
