using System;

namespace EcewsPortal.Models;

public sealed class ActiveUsersStats
{
	public int ActiveNow { get; set; }

	public int IdleNow { get; set; }

	public int SignInsToday { get; set; }

	public int UniqueUsersToday { get; set; }

	public int PeakConcurrent { get; set; }

	public DateTimeOffset? MostRecentSignIn { get; set; }

	public int MultiIpAlerts { get; set; }
}
