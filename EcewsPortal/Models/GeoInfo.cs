using System.Linq;

namespace EcewsPortal.Models;

public sealed class GeoInfo
{
	public string? Country { get; set; }

	public string? Region { get; set; }

	public string? City { get; set; }

	public string Label => (string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(Country)) ? "Unknown" : string.Join(", ", new string[3] { City, Region, Country }.Where((string s) => !string.IsNullOrWhiteSpace(s)));
}
