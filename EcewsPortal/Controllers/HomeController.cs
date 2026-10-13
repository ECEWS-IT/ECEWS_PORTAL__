using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;
using EcewsPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EcewsPortal.Controllers;

[Authorize]
public class HomeController(IPortalService portalService, INewsletterArticleStore articles, INotificationStore notifications, IPortalUserStore portalUsers, IWeatherService weather, IAdminAccessService adminAccess) : Controller
{
	public async Task<IActionResult> Index(CancellationToken ct)
	{
		bool isAdmin = adminAccess.IsAdmin(User);
		string email = ResolveEmail();
		PortalViewModel model = portalService.GetPortalData(email, isAdmin);
		ApplyUser(model);
		ApplyNotifications(model, email);
		await ApplyLiveWeatherAsync(model, ct);
		return View(model);
	}

	[HttpGet]
	public IActionResult Profile()
	{
		string text = ResolveEmail();
		string text2 = ResolveDisplayName();
		PortalUserStats stats = portalUsers.GetStats(text);
		int count = portalService.GetAppsForUser(text, adminAccess.IsAdmin(User)).Count;
		return View(new ProfileViewModel
		{
			DisplayName = text2,
			Initials = MakeInitials(text2),
			Email = text,
			IsAdmin = adminAccess.IsAdmin(User),
			JobHint = ((stats == null) ? "Signed in with Microsoft Entra ID" : "Portal sign-in account"),
			FirstSeenAt = stats?.FirstSeenAt,
			LastSeenAt = stats?.LastSeenAt,
			SignInCount = (stats?.SignInCount ?? 0),
			AssignedAppCount = count
		});
	}

	[HttpGet]
	public IActionResult Notifications()
	{
		string email = ResolveEmail();
		IReadOnlyList<PortalNotification> all = notifications.GetAll();
		int unreadCount = notifications.GetUnreadCount(email);
		notifications.MarkAllRead(email);
		return View(new NotificationsPageModel
		{
			Items = all,
			UnreadCount = unreadCount
		});
	}

	[HttpGet]
	public async Task<IActionResult> Weather(CancellationToken ct)
	{
		WeatherInfo live = await weather.GetForecastAsync(null, null, null, 7, "lagos", ct);
		WeatherInfo fallback = portalService.GetPortalData(ResolveEmail(), adminAccess.IsAdmin(User)).Weather;
		List<WeatherPlaceOption> places = (from p in NigeriaWeatherLocations.Places.OrderBy((NigeriaWeatherLocations.Place p) => p.State, StringComparer.OrdinalIgnoreCase)
			select new WeatherPlaceOption
			{
				Id = p.Id,
				Label = NigeriaWeatherLocations.FormatLabel(p),
				State = p.State,
				Lat = p.Lat,
				Lon = p.Lon
			}).ToList();
		return View(new WeatherPageModel
		{
			Weather = (live ?? fallback),
			Places = places
		});
	}

	[HttpGet]
	public IActionResult Privacy()
	{
		return View();
	}

	[HttpGet]
	public IActionResult Article(string id)
	{
		NewsletterArticle article = articles.GetBySlug(id);
		if (article == null)
		{
			return NotFound();
		}
		List<NewsletterArticle> related = (from a in articles.GetAll()
			where !string.Equals(a.Slug, article.Slug, StringComparison.OrdinalIgnoreCase)
			select a).Take(2).ToList();
		return View(new NewsletterArticleViewModel
		{
			Article = article,
			Related = related
		});
	}

	[AllowAnonymous]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public IActionResult Error([FromServices] IConfiguration config, [FromServices] IWebHostEnvironment env, [FromServices] ILogger<HomeController> logger)
	{
		bool value = config.GetValue("Portal:ShowDetailedErrors", defaultValue: false);
		IExceptionHandlerPathFeature exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
		Exception ex = exceptionHandlerPathFeature?.Error;
		if (ex != null)
		{
			logger.LogError(ex, "Unhandled exception at {Path}", exceptionHandlerPathFeature?.Path);
			try
			{
				string text = Path.Combine(env.ContentRootPath, "logs");
				Directory.CreateDirectory(text);
				System.IO.File.AppendAllText(Path.Combine(text, "exceptions.log"), $"[{DateTimeOffset.Now:u}] {exceptionHandlerPathFeature?.Path}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
			}
			catch
			{
			}
		}
		return View(new ErrorViewModel
		{
			RequestId = (Activity.Current?.Id ?? HttpContext.TraceIdentifier),
			ShowDetails = (value && ex != null),
			Message = ((ex == null) ? null : (ex.GetType().Name + ": " + ex.Message)),
			Detail = ((!value) ? null : ex?.ToString())
		});
	}

	private async Task ApplyLiveWeatherAsync(PortalViewModel model, CancellationToken ct)
	{
		try
		{
			WeatherInfo live = await weather.GetForecastAsync(null, null, null, 5, null, ct);
			if (live != null)
			{
				model.Weather = live;
			}
		}
		catch
		{
		}
	}

	private void ApplyNotifications(PortalViewModel model, string email)
	{
		IReadOnlyList<PortalNotification> all = notifications.GetAll();
		model.NotificationCount = notifications.GetUnreadCount(email);
		model.News = all.Take(4).Select((PortalNotification n, int i) => new NewsItem
		{
			Rank = i + 1,
			Text = n.Title,
			Source = n.Source + " · " + FormatRelative(n.CreatedAt),
			Url = n.Url
		}).ToList();
		ViewBag.Notifications = all.Take(5).ToList();
	}

	private void ApplyUser(PortalViewModel model)
	{
		string name = (model.StaffName = ResolveDisplayName());
		model.StaffInitials = MakeInitials(name);
		model.StaffEmail = ResolveEmail();
		model.IsAdmin = adminAccess.IsAdmin(User);
	}

	private string ResolveEmail()
	{
		return User.FindFirstValue("preferred_username") ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn") ?? string.Empty;
	}

	private string ResolveDisplayName()
	{
		string text = User.FindFirstValue("name");
		string text2 = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name") ?? User.Identity?.Name ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		if (!string.IsNullOrWhiteSpace(text2) && text2.Contains('@'))
		{
			string text3 = text2.Split('@')[0].Replace('.', ' ');
			return string.Join(' ', text3.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((string p) =>
			{
				ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(char.ToUpperInvariant(p[0]));
				string text4;
				if (p.Length <= 1)
				{
					text4 = string.Empty;
				}
				else
				{
					text4 = p.Substring(1, p.Length - 1);
				}
				return string.Concat(readOnlySpan, text4);
			}));
		}
		if (!string.IsNullOrWhiteSpace(text2))
		{
			return text2;
		}
		return "ECEWS Staff";
	}

	private static string MakeInitials(string name)
	{
		string[] array = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0)
		{
			return "ES";
		}
		if (array.Length == 1)
		{
			return array[0].Substring(0, Math.Min(2, array[0].Length)).ToUpperInvariant();
		}
		return $"{char.ToUpperInvariant(array[0][0])}{char.ToUpperInvariant(array[^1][0])}";
	}

	private static string FormatRelative(DateTimeOffset when)
	{
		TimeSpan timeSpan = DateTimeOffset.Now - when;
		if (timeSpan.TotalMinutes < 60.0)
		{
			return $"{Math.Max(1, (int)timeSpan.TotalMinutes)}m ago";
		}
		if (timeSpan.TotalHours < 24.0)
		{
			return $"{(int)timeSpan.TotalHours}h ago";
		}
		if (timeSpan.TotalDays < 7.0)
		{
			return $"{(int)timeSpan.TotalDays}d ago";
		}
		return when.LocalDateTime.ToString("d MMM");
	}
}
