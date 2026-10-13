using System;

namespace EcewsPortal.Models;

public sealed class ActiveUsersViewModel
{
	public ActiveUsersSnapshot Snapshot { get; init; } = new ActiveUsersSnapshot();

	public TimeSpan IdleTimeout { get; init; }
}
