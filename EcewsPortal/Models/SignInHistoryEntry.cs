using System;

namespace EcewsPortal.Models;

public sealed class SignInHistoryEntry
{
	public string Email { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public DateTimeOffset SignedInAt { get; set; }

	public string? IpAddress { get; set; }

	public string DeviceLabel { get; set; } = "Unknown";
}
