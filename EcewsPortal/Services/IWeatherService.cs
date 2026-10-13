using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface IWeatherService
{
	Task<WeatherInfo?> GetForecastAsync(double? lat = null, double? lon = null, string? timezone = null, int days = 5, string? locationName = null, CancellationToken ct = default(CancellationToken));

	IReadOnlyList<object> GetLocationCatalog();
}
