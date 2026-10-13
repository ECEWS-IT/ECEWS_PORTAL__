using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace EcewsPortal.Services;

public sealed class AnalyticsStore : IAnalyticsStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly object _lock = new object();

	private readonly List<AnalyticsEvent> _events = new List<AnalyticsEvent>();

	private readonly string _path;

	private readonly IGeoLocationService _geo;

	public AnalyticsStore(IWebHostEnvironment env, IGeoLocationService geo)
	{
		_geo = geo;
		string text = Path.Combine(env.ContentRootPath, "App_Data");
		Directory.CreateDirectory(text);
		_path = Path.Combine(text, "analytics-events.json");
		Load();
	}

	public async Task RecordLoginAsync(ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct = default(CancellationToken))
	{
		string email = GetEmail(user);
		if (!string.IsNullOrWhiteSpace(email))
		{
			string ip = httpContext.Connection.RemoteIpAddress?.ToString();
			GeoInfo geo = await _geo.ResolveAsync(ip, ct);
			AnalyticsEvent evt = new AnalyticsEvent
			{
				Type = "login",
				At = DateTimeOffset.UtcNow,
				Email = email,
				DisplayName = GetDisplayName(user),
				IpAddress = ip,
				Country = geo.Country,
				Region = geo.Region,
				City = geo.City
			};
			Append(evt);
		}
	}

	public async Task RecordAppVisitAsync(ClaimsPrincipal user, HttpContext httpContext, int appId, string appName, CancellationToken ct = default(CancellationToken))
	{
		string email = GetEmail(user);
		string ip = httpContext.Connection.RemoteIpAddress?.ToString();
		GeoInfo geo = await _geo.ResolveAsync(ip, ct);
		AnalyticsEvent evt = new AnalyticsEvent
		{
			Type = "app_visit",
			At = DateTimeOffset.UtcNow,
			Email = (string.IsNullOrWhiteSpace(email) ? null : email),
			DisplayName = GetDisplayName(user),
			AppId = appId,
			AppName = (string.IsNullOrWhiteSpace(appName) ? $"App {appId}" : appName.Trim()),
			IpAddress = ip,
			Country = geo.Country,
			Region = geo.Region,
			City = geo.City
		};
		Append(evt);
	}

	public AnalyticsDashboardDto GetDashboard(IReadOnlyList<AppItem> catalogApps, int activeUsersNow)
	{
		List<AnalyticsEvent> source;
		lock (_lock)
		{
			source = _events.ToList();
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		DateTimeOffset startToday = new DateTimeOffset(utcNow.Date, TimeSpan.Zero);
		DateTimeOffset startWeek = startToday.AddDays(-6.0);
		DateTimeOffset start = startToday.AddDays(-13.0);
		List<AnalyticsEvent> source2 = source.Where((AnalyticsEvent e) => e.Type == "login").ToList();
		List<AnalyticsEvent> source3 = source.Where((AnalyticsEvent e) => e.Type == "app_visit").ToList();
		int loginsToday = source2.Count((AnalyticsEvent e) => e.At >= startToday);
		int loginsThisWeek = source2.Count((AnalyticsEvent e) => e.At >= startWeek);
		int appVisitsToday = source3.Count((AnalyticsEvent e) => e.At >= startToday);
		int uniqueVisitorsToday = (from e in source
			where e.At >= startToday && !string.IsNullOrWhiteSpace(e.Email)
			select e.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count();
		List<NamedCount> list = (from g in (from e in source
				where e.At >= startWeek
				select string.IsNullOrWhiteSpace(e.LocationLabel) ? "Unknown" : e.LocationLabel).GroupBy((string l) => l, StringComparer.OrdinalIgnoreCase)
			select new NamedCount
			{
				Name = g.Key,
				Count = g.Count()
			} into x
			orderby x.Count descending
			select x).Take(10).ToList();
		List<NamedCount> source4 = (from g in source3.GroupBy((AnalyticsEvent v) => v.AppName ?? $"App {v.AppId}", StringComparer.OrdinalIgnoreCase)
			select new NamedCount
			{
				Name = g.Key,
				Count = g.Count()
			}).ToList();
		List<NamedCount> mostVisitedApps = source4.OrderByDescending((NamedCount x) => x.Count).Take(10).ToList();
		Dictionary<string, int> visitLookup = source4.ToDictionary((NamedCount x) => x.Name, (NamedCount x) => x.Count, StringComparer.OrdinalIgnoreCase);
		List<NamedCount> leastVisitedApps = (from a in catalogApps
			select new NamedCount
			{
				Name = a.Name,
				Count = (visitLookup.TryGetValue(a.Name, out var value) ? value : 0)
			} into x
			orderby x.Count, x.Name
			select x).Take(10).ToList();
		return new AnalyticsDashboardDto
		{
			LoginsToday = loginsToday,
			LoginsThisWeek = loginsThisWeek,
			ActiveUsersNow = activeUsersNow,
			AppVisitsToday = appVisitsToday,
			UniqueVisitorsToday = uniqueVisitorsToday,
			TopLocation = (list.FirstOrDefault()?.Name ?? "—"),
			LoginsByDay = BuildSeries(source2, start, 14),
			VisitsByDay = BuildSeries(source3, start, 14),
			MostVisitedApps = mostVisitedApps,
			LeastVisitedApps = leastVisitedApps,
			Locations = list,
			RecentEvents = source.OrderByDescending((AnalyticsEvent e) => e.At).Take(40).ToList(),
			GeneratedAt = utcNow
		};
	}

	private static List<TimeSeriesPoint> BuildSeries(IEnumerable<AnalyticsEvent> source, DateTimeOffset start, int days)
	{
		Dictionary<DateTime, int> dictionary = (from e in source
			where e.At >= start
			group e by e.At.UtcDateTime.Date).ToDictionary((IGrouping<DateTime, AnalyticsEvent> g) => g.Key, (IGrouping<DateTime, AnalyticsEvent> g) => g.Count());
		List<TimeSeriesPoint> list = new List<TimeSeriesPoint>(days);
		for (int num = 0; num < days; num++)
		{
			DateTime key = start.UtcDateTime.Date.AddDays(num);
			list.Add(new TimeSeriesPoint
			{
				Date = key.ToString("MMM d"),
				Count = (dictionary.TryGetValue(key, out var value) ? value : 0)
			});
		}
		return list;
	}

	private void Append(AnalyticsEvent evt)
	{
		lock (_lock)
		{
			if (evt.Type == "login" && !string.IsNullOrWhiteSpace(evt.Email))
			{
				AnalyticsEvent analyticsEvent = _events.LastOrDefault((AnalyticsEvent e) => e.Type == "login" && string.Equals(e.Email, evt.Email, StringComparison.OrdinalIgnoreCase));
				if (analyticsEvent != null && evt.At - analyticsEvent.At < TimeSpan.FromSeconds(90L))
				{
					return;
				}
			}
			_events.Add(evt);
			DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-90.0);
			_events.RemoveAll((AnalyticsEvent e) => e.At < cutoff);
			if (_events.Count > 12000)
			{
				_events.RemoveRange(0, _events.Count - 12000);
			}
			SaveUnlocked();
		}
	}

	private void Load()
	{
		try
		{
			if (!File.Exists(_path))
			{
				return;
			}
			string json = File.ReadAllText(_path);
			List<AnalyticsEvent> list = JsonSerializer.Deserialize<List<AnalyticsEvent>>(json);
			if (list == null || list.Count <= 0)
			{
				return;
			}
			lock (_lock)
			{
				_events.Clear();
				_events.AddRange(list.OrderBy((AnalyticsEvent e) => e.At).TakeLast(12000));
			}
		}
		catch
		{
		}
	}

	private void SaveUnlocked()
	{
		try
		{
			File.WriteAllText(_path, JsonSerializer.Serialize(_events, JsonOptions));
		}
		catch
		{
		}
	}

	private static string GetEmail(ClaimsPrincipal user)
	{
		return user.FindFirstValue("preferred_username") ?? user.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? user.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn") ?? string.Empty;
	}

	private static string GetDisplayName(ClaimsPrincipal user)
	{
		return user.FindFirstValue("name") ?? user.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name") ?? GetEmail(user);
	}
}
