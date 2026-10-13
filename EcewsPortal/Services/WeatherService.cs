using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public sealed class WeatherService : IWeatherService
{
	private sealed class OpenMeteoForecastResponse
	{
		[JsonPropertyName("current")]
		public CurrentBlock? Current { get; set; }

		[JsonPropertyName("daily")]
		public DailyBlock? Daily { get; set; }
	}

	private sealed class CurrentBlock
	{
		[JsonPropertyName("temperature_2m")]
		public double Temperature2m { get; set; }

		[JsonPropertyName("relative_humidity_2m")]
		public double? RelativeHumidity2m { get; set; }

		[JsonPropertyName("weather_code")]
		public int WeatherCode { get; set; }
	}

	private sealed class DailyBlock
	{
		[JsonPropertyName("time")]
		public List<string> Time { get; set; } = new List<string>();

		[JsonPropertyName("weather_code")]
		public List<int> WeatherCode { get; set; } = new List<int>();

		[JsonPropertyName("temperature_2m_max")]
		public List<double> TemperatureMax { get; set; } = new List<double>();

		[JsonPropertyName("temperature_2m_min")]
		public List<double> TemperatureMin { get; set; } = new List<double>();
	}

	private const string LagosTimezone = "Africa/Lagos";

	private readonly HttpClient _http;

	private readonly ConcurrentDictionary<string, (WeatherInfo Info, DateTimeOffset At)> _cache = new ConcurrentDictionary<string, (WeatherInfo, DateTimeOffset)>();

	public WeatherService(HttpClient http)
	{
		_http = http;
	}

	public IReadOnlyList<object> GetLocationCatalog()
	{
		return ((IEnumerable<object>)(from p in NigeriaWeatherLocations.Places.OrderBy((NigeriaWeatherLocations.Place p) => p.State, StringComparer.OrdinalIgnoreCase)
			select new
			{
				id = p.Id,
				name = p.Name,
				state = p.State,
				label = NigeriaWeatherLocations.FormatLabel(p),
				lat = p.Lat,
				lon = p.Lon
			})).ToList();
	}

	public async Task<WeatherInfo?> GetForecastAsync(double? lat = null, double? lon = null, string? timezone = null, int days = 5, string? locationName = null, CancellationToken ct = default(CancellationToken))
	{
		int forecastDays = Math.Clamp(days, 1, 7);
		NigeriaWeatherLocations.Place place = NigeriaWeatherLocations.FindById(locationName);
		double latitude;
		double longitude;
		string displayName;
		if ((object)place != null)
		{
			latitude = place.Lat;
			longitude = place.Lon;
			displayName = NigeriaWeatherLocations.FormatLabel(place);
		}
		else if (lat.HasValue && lon.HasValue)
		{
			latitude = lat.Value;
			longitude = lon.Value;
			NigeriaWeatherLocations.Place nearest = NigeriaWeatherLocations.Nearest(latitude, longitude);
			displayName = (string.IsNullOrWhiteSpace(locationName) ? ("Near " + NigeriaWeatherLocations.FormatLabel(nearest)) : locationName.Trim());
		}
		else
		{
			place = NigeriaWeatherLocations.FindById("lagos");
			latitude = place.Lat;
			longitude = place.Lon;
			displayName = NigeriaWeatherLocations.FormatLabel(place);
		}
		string tz = (string.IsNullOrWhiteSpace(timezone) ? "Africa/Lagos" : timezone.Trim());
		string cacheKey = $"{latitude:F3},{longitude:F3}:{forecastDays}";
		if (_cache.TryGetValue(cacheKey, out var cached) && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromMinutes(10L))
		{
			WeatherInfo hit = Clone(cached.Info, forecastDays);
			hit.Location = displayName;
			return hit;
		}
		string url = "https://api.open-meteo.com/v1/forecast?latitude=" + latitude.ToString(CultureInfo.InvariantCulture) + "&longitude=" + longitude.ToString(CultureInfo.InvariantCulture) + "&current=temperature_2m,relative_humidity_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,weather_code&timezone=" + Uri.EscapeDataString(tz) + "&forecast_days=" + forecastDays;
		OpenMeteoForecastResponse result;
		try
		{
			result = await _http.GetFromJsonAsync<OpenMeteoForecastResponse>(url, ct);
		}
		catch
		{
			(WeatherInfo Info, DateTimeOffset At) stale;
			return _cache.TryGetValue(cacheKey, out stale) ? Clone(stale.Info, forecastDays) : null;
		}
		if (result?.Current == null || result.Daily?.Time == null || result.Daily.Time.Count == 0)
		{
			return null;
		}
		double? humidity = result.Current.RelativeHumidity2m;
		int code = result.Current.WeatherCode;
		List<DailyForecast> daysList = new List<DailyForecast>();
		int count = Math.Min(forecastDays, result.Daily.Time.Count);
		for (int i = 0; i < count; i++)
		{
			string dayLabel = Convert.ToDateTime(result.Daily.Time[i], CultureInfo.InvariantCulture).ToString("ddd", CultureInfo.InvariantCulture);
			daysList.Add(new DailyForecast
			{
				Day = dayLabel,
				Icon = WeatherCodeToEmoji(result.Daily.WeatherCode[i]),
				High = (int)Math.Round(result.Daily.TemperatureMax[i]) + "°",
				Low = (int)Math.Round(result.Daily.TemperatureMin[i]) + "°"
			});
		}
		WeatherInfo info = new WeatherInfo
		{
			Location = displayName,
			Temperature = (int)Math.Round(result.Current.Temperature2m) + "°C",
			Description = BuildWeatherDescription(code, humidity),
			Icon = WeatherCodeToEmoji(code),
			Forecast = daysList
		};
		_cache[cacheKey] = (info, DateTimeOffset.UtcNow);
		return Clone(info, forecastDays);
	}

	private static WeatherInfo Clone(WeatherInfo src, int days)
	{
		return new WeatherInfo
		{
			Location = src.Location,
			Temperature = src.Temperature,
			Description = src.Description,
			Icon = src.Icon,
			Forecast = src.Forecast.Take(days).ToList()
		};
	}

	private static string BuildWeatherDescription(int weatherCode, double? humidity)
	{
		string text = WeatherCodeToText(weatherCode);
		string text2 = (humidity.HasValue ? Math.Round(humidity.Value).ToString(CultureInfo.InvariantCulture) : null);
		return (text2 == null) ? text : (text + " · Humidity " + text2 + "%");
	}

	private static string WeatherCodeToText(int code)
	{
		if (1 == 0)
		{
		}
		string result;
		switch (code)
		{
		case 0:
			result = "Clear sky";
			break;
		case 1:
			result = "Mainly clear";
			break;
		case 2:
			result = "Partly cloudy";
			break;
		case 3:
			result = "Overcast";
			break;
		case 45:
		case 48:
			result = "Fog";
			break;
		case 51:
		case 53:
		case 55:
			result = "Light drizzle";
			break;
		case 56:
		case 57:
			result = "Freezing drizzle";
			break;
		case 61:
		case 63:
		case 65:
			result = "Rain";
			break;
		case 66:
		case 67:
			result = "Freezing rain";
			break;
		case 71:
		case 73:
		case 75:
		case 77:
			result = "Snow";
			break;
		case 80:
		case 81:
			result = "Rain showers";
			break;
		case 82:
			result = "Heavy rain showers";
			break;
		case 85:
		case 86:
			result = "Snow showers";
			break;
		case 95:
		case 96:
		case 99:
			result = "Thunderstorms";
			break;
		default:
			result = "Weather update";
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private static string WeatherCodeToEmoji(int code)
	{
		if (1 == 0)
		{
		}
		string result;
		switch (code)
		{
		case 0:
			result = "☀\ufe0f";
			break;
		case 1:
			result = "\ud83c\udf24";
			break;
		case 2:
			result = "⛅";
			break;
		case 3:
			result = "☁\ufe0f";
			break;
		case 45:
		case 48:
			result = "\ud83c\udf2b";
			break;
		case 51:
		case 53:
		case 55:
			result = "\ud83c\udf26";
			break;
		case 56:
		case 57:
			result = "\ud83c\udf27";
			break;
		case 61:
		case 63:
		case 65:
			result = "\ud83c\udf27";
			break;
		case 66:
		case 67:
			result = "\ud83c\udf27";
			break;
		case 71:
		case 73:
		case 75:
		case 77:
			result = "\ud83c\udf28";
			break;
		case 80:
		case 81:
			result = "\ud83c\udf26";
			break;
		case 82:
			result = "\ud83c\udf27";
			break;
		case 85:
		case 86:
			result = "\ud83c\udf28";
			break;
		case 95:
		case 96:
		case 99:
			result = "⛈";
			break;
		default:
			result = "⛅";
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}
}
