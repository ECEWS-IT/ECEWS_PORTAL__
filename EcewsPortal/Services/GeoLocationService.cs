using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public sealed class GeoLocationService : IGeoLocationService
{
	private sealed class IpApiDto
	{
		[JsonPropertyName("status")]
		public string? Status { get; set; }

		[JsonPropertyName("country")]
		public string? Country { get; set; }

		[JsonPropertyName("regionName")]
		public string? RegionName { get; set; }

		[JsonPropertyName("city")]
		public string? City { get; set; }
	}

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(4L)
	};

	private readonly ConcurrentDictionary<string, GeoInfo> _cache = new ConcurrentDictionary<string, GeoInfo>(StringComparer.OrdinalIgnoreCase);

	public async Task<GeoInfo> ResolveAsync(string? ipAddress, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(ipAddress) || IsLocal(ipAddress))
		{
			return new GeoInfo
			{
				City = "Local",
				Region = "Development",
				Country = "Local"
			};
		}
		if (_cache.TryGetValue(ipAddress, out GeoInfo cached))
		{
			return cached;
		}
		try
		{
			string url = "http://ip-api.com/json/" + Uri.EscapeDataString(ipAddress) + "?fields=status,country,regionName,city";
			using HttpResponseMessage resp = await Http.GetAsync(url, ct);
			if (!resp.IsSuccessStatusCode)
			{
				return Cache(ipAddress, new GeoInfo
				{
					Country = "Unknown"
				});
			}
			GeoInfo result;
			await using (Stream stream = await resp.Content.ReadAsStreamAsync(ct))
			{
				IpApiDto dto = await JsonSerializer.DeserializeAsync<IpApiDto>(stream, (JsonSerializerOptions?)null, ct);
				result = ((dto != null && string.Equals(dto.Status, "success", StringComparison.OrdinalIgnoreCase)) ? Cache(ipAddress, new GeoInfo
				{
					Country = dto.Country,
					Region = dto.RegionName,
					City = dto.City
				}) : Cache(ipAddress, new GeoInfo
				{
					Country = "Unknown"
				}));
			}
			return result;
		}
		catch
		{
			return Cache(ipAddress, new GeoInfo
			{
				Country = "Unknown"
			});
		}
	}

	private GeoInfo Cache(string ip, GeoInfo info)
	{
		_cache[ip] = info;
		return info;
	}

	private static bool IsLocal(string ip)
	{
		bool flag;
		switch (ip)
		{
		case "127.0.0.1":
		case "::1":
		case "localhost":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return true;
		}
		if (IPAddress.TryParse(ip, out IPAddress address))
		{
			if (IPAddress.IsLoopback(address))
			{
				return true;
			}
			byte[] addressBytes = address.GetAddressBytes();
			if (address.AddressFamily == AddressFamily.InterNetwork)
			{
				if (addressBytes[0] == 10)
				{
					return true;
				}
				if (addressBytes[0] == 192 && addressBytes[1] == 168)
				{
					return true;
				}
				if (addressBytes[0] == 172 && addressBytes[1] >= 16 && addressBytes[1] <= 31)
				{
					return true;
				}
			}
		}
		return false;
	}
}
