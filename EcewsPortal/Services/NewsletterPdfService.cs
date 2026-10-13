using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EcewsPortal.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcewsPortal.Services;

public sealed class NewsletterPdfService : INewsletterPdfService, IDisposable
{
	private sealed class NewsletterCandidate
	{
		public string SourcePath { get; init; } = string.Empty;

		public string FileName { get; init; } = string.Empty;

		public DateTime LastWriteTimeUtc { get; init; }

		public long SizeBytes { get; init; }

		public string SortKey { get; init; } = string.Empty;
	}

	private readonly IWebHostEnvironment _env;

	private readonly NewsletterOptions _options;

	private readonly ILogger<NewsletterPdfService> _logger;

	private readonly object _gate = new object();

	private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();

	private NewsletterEdition? _current;

	private string? _publishedFingerprint;

	public NewsletterEdition? Current
	{
		get
		{
			lock (_gate)
			{
				return _current;
			}
		}
	}

	public string PublicPdfUrl => Current?.PublicUrl ?? _options.PublicPath;

	public NewsletterPdfService(IWebHostEnvironment env, IOptions<NewsletterOptions> options, ILogger<NewsletterPdfService> logger)
	{
		_env = env;
		_options = options.Value;
		_logger = logger;
		Refresh(forceCopy: true);
		StartWatchers();
	}

	public string GetPdfUrlForPage(int page)
	{
		string publicPdfUrl = PublicPdfUrl;
		if (page > 0)
		{
			return $"{publicPdfUrl}#page={page}";
		}
		return publicPdfUrl;
	}

	public NewsletterEdition? Refresh(bool forceCopy = false)
	{
		lock (_gate)
		{
			NewsletterCandidate newsletterCandidate = FindLatest();
			if (newsletterCandidate == null)
			{
				_logger.LogWarning("No newsletter PDF matching {Pattern} was found.", _options.FilePattern);
				return _current;
			}
			string text = $"{newsletterCandidate.SourcePath}|{newsletterCandidate.LastWriteTimeUtc:O}|{newsletterCandidate.SizeBytes}";
			string text2 = Path.Combine(_env.WebRootPath, "newsletters", _options.PublishedFileName);
			Directory.CreateDirectory(Path.GetDirectoryName(text2));
			if (forceCopy || !string.Equals(_publishedFingerprint, text, StringComparison.Ordinal) || !File.Exists(text2))
			{
				if (!string.Equals(Path.GetFullPath(newsletterCandidate.SourcePath), Path.GetFullPath(text2), StringComparison.OrdinalIgnoreCase))
				{
					string text3 = text2 + ".tmp";
					File.Copy(newsletterCandidate.SourcePath, text3, overwrite: true);
					File.Copy(text3, text2, overwrite: true);
					try
					{
						File.Delete(text3);
					}
					catch
					{
					}
				}
				_publishedFingerprint = text;
				_logger.LogInformation("Newsletter edition active: {File} ({Size} bytes, {When:u})", newsletterCandidate.FileName, newsletterCandidate.SizeBytes, newsletterCandidate.LastWriteTimeUtc);
			}
			_current = new NewsletterEdition
			{
				SourcePath = newsletterCandidate.SourcePath,
				FileName = newsletterCandidate.FileName,
				LastWriteTimeUtc = newsletterCandidate.LastWriteTimeUtc,
				SizeBytes = newsletterCandidate.SizeBytes,
				PublicUrl = _options.PublicPath
			};
			WriteStateFile(_current);
			return _current;
		}
	}

	private NewsletterCandidate? FindLatest()
	{
		string searchPattern = (string.IsNullOrWhiteSpace(_options.FilePattern) ? "ECEWS-Rising*.pdf" : _options.FilePattern);
		List<NewsletterCandidate> list = new List<NewsletterCandidate>();
		foreach (string item in ResolveWatchFolders())
		{
			if (!Directory.Exists(item))
			{
				continue;
			}
			foreach (string item2 in Directory.EnumerateFiles(item, searchPattern, SearchOption.TopDirectoryOnly))
			{
				string fileName = Path.GetFileName(item2);
				if (!string.Equals(fileName, _options.PublishedFileName, StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
				{
					FileInfo fileInfo = new FileInfo(item2);
					list.Add(new NewsletterCandidate
					{
						SourcePath = fileInfo.FullName,
						FileName = fileInfo.Name,
						LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
						SizeBytes = fileInfo.Length,
						SortKey = BuildSortKey(fileInfo.Name, fileInfo.LastWriteTimeUtc)
					});
				}
			}
		}
		return (from c in list
			orderby c.SortKey descending, c.LastWriteTimeUtc descending
			select c).FirstOrDefault();
	}

	private static string BuildSortKey(string fileName, DateTime lastWriteUtc)
	{
		int value = 0;
		int value2 = 0;
		Match match = Regex.Match(fileName, "(?i)(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?).*?(20\\d{2})");
		if (match.Success)
		{
			value = int.Parse(match.Groups[2].Value);
			value2 = MonthNumber(match.Groups[1].Value);
		}
		else
		{
			Match match2 = Regex.Match(fileName, "(20\\d{2})");
			if (match2.Success)
			{
				value = int.Parse(match2.Groups[1].Value);
			}
		}
		return $"{value:D4}-{value2:D2}-{lastWriteUtc.Ticks:D20}";
	}

	private static int MonthNumber(string token)
	{
		token = token.ToLowerInvariant();
		if (1 == 0)
		{
		}
		int result;
		switch (token)
		{
		case "jan":
		case "january":
			result = 1;
			break;
		case "feb":
		case "february":
			result = 2;
			break;
		case "mar":
		case "march":
			result = 3;
			break;
		case "apr":
		case "april":
			result = 4;
			break;
		case "may":
			result = 5;
			break;
		case "jun":
		case "june":
			result = 6;
			break;
		case "jul":
		case "july":
			result = 7;
			break;
		case "aug":
		case "august":
			result = 8;
			break;
		case "sep":
		case "sept":
		case "september":
			result = 9;
			break;
		case "oct":
		case "october":
			result = 10;
			break;
		case "nov":
		case "november":
			result = 11;
			break;
		case "dec":
		case "december":
			result = 12;
			break;
		default:
			result = 0;
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private IEnumerable<string> ResolveWatchFolders()
	{
		foreach (string raw in _options.WatchFolders)
		{
			if (!string.IsNullOrWhiteSpace(raw))
			{
				string path = raw.Trim();
				if (!Path.IsPathRooted(path))
				{
					path = Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));
				}
				yield return path;
			}
		}
	}

	private void WriteStateFile(NewsletterEdition edition)
	{
		try
		{
			string text = Path.Combine(_env.ContentRootPath, "App_Data");
			Directory.CreateDirectory(text);
			string contents = "{\n  \"fileName\": \"" + edition.FileName.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\n  \"sourcePath\": \"" + edition.SourcePath.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\n" + $"  \"lastWriteTimeUtc\": \"{edition.LastWriteTimeUtc:O}\",\n" + "  \"publicUrl\": \"" + edition.PublicUrl + "\"\n}\n";
			File.WriteAllText(Path.Combine(text, "newsletter-current.json"), contents);
		}
		catch
		{
		}
	}

	private void StartWatchers()
	{
		foreach (string item in ResolveWatchFolders())
		{
			try
			{
				Directory.CreateDirectory(item);
				FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(item)
				{
					Filter = _options.FilePattern,
					NotifyFilter = (NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime),
					EnableRaisingEvents = true
				};
				fileSystemWatcher.Created += OnChanged;
				fileSystemWatcher.Changed += OnChanged;
				fileSystemWatcher.Renamed += OnChanged;
				fileSystemWatcher.Deleted += (object _, FileSystemEventArgs _) =>
				{
					Refresh(forceCopy: true);
				};
				_watchers.Add(fileSystemWatcher);
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Could not watch newsletter folder {Folder}", item);
			}
		}
	}

	private void OnChanged(object sender, FileSystemEventArgs e)
	{
		Task.Run(async () =>
		{
			await Task.Delay(800);
			try
			{
				Refresh();
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				_logger.LogWarning(ex2, "Newsletter refresh failed");
			}
		});
	}

	public void Dispose()
	{
		foreach (FileSystemWatcher watcher in _watchers)
		{
			watcher.EnableRaisingEvents = false;
			watcher.Dispose();
		}
		_watchers.Clear();
	}
}
