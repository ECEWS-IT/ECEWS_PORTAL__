using System;
using System.Collections.Generic;

namespace EcewsPortal.Models;

public sealed class ActiveUsersSnapshot
{
	public IReadOnlyList<ActiveUserSession> Users { get; init; } = Array.Empty<ActiveUserSession>();

	public IReadOnlyList<SignInHistoryEntry> History { get; init; } = Array.Empty<SignInHistoryEntry>();

	public ActiveUsersStats Stats { get; init; } = new ActiveUsersStats();

	public int IdleTimeoutMinutes { get; init; }

	public int IdleWarnMinutes { get; init; }

	public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}
