using System;
using System.Linq;

namespace EcewsPortal.Models;

public sealed class AnalyticsEvent
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string Type { get; set; } = string.Empty;

	public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

	public string? Email { get; set; }

	public string? DisplayName { get; set; }

	public int? AppId { get; set; }

	public string? AppName { get; set; }

	public string? IpAddress { get; set; }

	public string? Country { get; set; }

	public string? Region { get; set; }

	public string? City { get; set; }

	public string LocationLabel => string.Join(", ", new string[3] { City, Region, Country }.Where((string s) => !string.IsNullOrWhiteSpace(s)));
}
