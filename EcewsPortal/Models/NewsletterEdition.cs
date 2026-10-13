using System;

namespace EcewsPortal.Models;

public sealed class NewsletterEdition
{
	public string SourcePath { get; init; } = string.Empty;

	public string FileName { get; init; } = string.Empty;

	public DateTimeOffset LastWriteTimeUtc { get; init; }

	public long SizeBytes { get; init; }

	public string PublicUrl { get; init; } = string.Empty;
}
