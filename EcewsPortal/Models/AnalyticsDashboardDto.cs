using System;
using System.Collections.Generic;

namespace EcewsPortal.Models;

public sealed class AnalyticsDashboardDto
{
	public int LoginsToday { get; set; }

	public int LoginsThisWeek { get; set; }

	public int ActiveUsersNow { get; set; }

	public int AppVisitsToday { get; set; }

	public int UniqueVisitorsToday { get; set; }

	public string TopLocation { get; set; } = "—";

	public IReadOnlyList<TimeSeriesPoint> LoginsByDay { get; set; } = Array.Empty<TimeSeriesPoint>();

	public IReadOnlyList<TimeSeriesPoint> VisitsByDay { get; set; } = Array.Empty<TimeSeriesPoint>();

	public IReadOnlyList<NamedCount> MostVisitedApps { get; set; } = Array.Empty<NamedCount>();

	public IReadOnlyList<NamedCount> LeastVisitedApps { get; set; } = Array.Empty<NamedCount>();

	public IReadOnlyList<NamedCount> Locations { get; set; } = Array.Empty<NamedCount>();

	public IReadOnlyList<AnalyticsEvent> RecentEvents { get; set; } = Array.Empty<AnalyticsEvent>();

	public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
