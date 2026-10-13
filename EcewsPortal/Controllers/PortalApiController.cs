using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;
using EcewsPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcewsPortal.Controllers;

[ApiController]
[Route("api/portal")]
[Authorize]
public class PortalApiController(IPortalService portalService, IAnalyticsStore analytics, IWeatherService weather, IAdminAccessService adminAccess) : ControllerBase
{
	public sealed class TrackVisitRequest
	{
		public int AppId { get; set; }

		public string? AppName { get; set; }
	}

	[HttpGet("apps")]
	public IActionResult GetApps([FromQuery] string? search, [FromQuery] string? category)
	{
		string userEmail = User.FindFirstValue("preferred_username") ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn") ?? string.Empty;
		bool isAdmin = adminAccess.IsAdmin(User);
		IReadOnlyList<AppItem> readOnlyList = portalService.SearchApps(search, category, userEmail, isAdmin);
		return Ok(new
		{
			count = readOnlyList.Count,
			apps = readOnlyList
		});
	}

	[HttpGet("health")]
	public IActionResult Health()
	{
		return Ok(new
		{
			status = "operational",
			timestamp = DateTime.UtcNow
		});
	}

	[HttpPost("track-visit")]
	public async Task<IActionResult> TrackVisit([FromBody] TrackVisitRequest? body, CancellationToken ct)
	{
		if (body == null || body.AppId <= 0)
		{
			return BadRequest(new
			{
				ok = false,
				error = "appId is required"
			});
		}
		string name = body.AppName;
		if (string.IsNullOrWhiteSpace(name))
		{
			name = portalService.GetAllApps().FirstOrDefault((AppItem a) => a.Id == body.AppId)?.Name ?? $"App {body.AppId}";
		}
		await analytics.RecordAppVisitAsync(User, HttpContext, body.AppId, name, ct);
		return Ok(new
		{
			ok = true
		});
	}

	[HttpGet("weather")]
	public async Task<IActionResult> Weather([FromQuery] double? lat, [FromQuery] double? lon, [FromQuery] string? timezone, [FromQuery] string? state, [FromQuery] string? name, [FromQuery] int days = 5, CancellationToken ct = default(CancellationToken))
	{
		try
		{
			string locationKey = state ?? name;
			WeatherInfo data = await weather.GetForecastAsync(lat, lon, timezone, days, locationKey, ct);
			if (data == null)
			{
				return StatusCode(502, new
				{
					error = "Weather provider returned no data"
				});
			}
			return Ok(new
			{
				location = data.Location,
				temperature = data.Temperature,
				description = data.Description,
				icon = data.Icon,
				forecast = data.Forecast.Select((DailyForecast d) => new
				{
					day = d.Day,
					icon = d.Icon,
					high = d.High,
					low = d.Low
				}),
				live = true,
				updatedAt = DateTimeOffset.Now
			});
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			return StatusCode(502, new
			{
				error = ex2.Message
			});
		}
	}

	[HttpGet("weather/locations")]
	public IActionResult WeatherLocations()
	{
		return Ok(new
		{
			locations = weather.GetLocationCatalog()
		});
	}
}
