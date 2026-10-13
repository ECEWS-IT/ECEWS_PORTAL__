using System;
using System.Collections.Generic;
using System.Linq;

namespace EcewsPortal.Services;

public static class NigeriaWeatherLocations
{
	public sealed record Place(string Id, string Name, string State, double Lat, double Lon);

	public static IReadOnlyList<Place> Places { get; } = new Place[]
	{
		new Place("fct", "Abuja", "FCT", 9.0765, 7.3986),
		new Place("abia", "Umuahia", "Abia", 5.5263, 7.489),
		new Place("adamawa", "Yola", "Adamawa", 9.2035, 12.4954),
		new Place("akwa-ibom", "Uyo", "Akwa Ibom", 5.0377, 7.9128),
		new Place("anambra", "Awka", "Anambra", 6.21, 7.074),
		new Place("bauchi", "Bauchi", "Bauchi", 10.3158, 9.8442),
		new Place("bayelsa", "Yenagoa", "Bayelsa", 4.9267, 6.2676),
		new Place("benue", "Makurdi", "Benue", 7.7322, 8.5391),
		new Place("borno", "Maiduguri", "Borno", 11.8311, 13.151),
		new Place("cross-river", "Calabar", "Cross River", 4.9757, 8.3417),
		new Place("delta", "Asaba", "Delta", 6.1982, 6.7319),
		new Place("ebonyi", "Abakaliki", "Ebonyi", 6.3249, 8.1135),
		new Place("edo", "Benin City", "Edo", 6.335, 5.6037),
		new Place("ekiti", "Ado-Ekiti", "Ekiti", 7.6233, 5.2209),
		new Place("enugu", "Enugu", "Enugu", 6.4584, 7.5464),
		new Place("gombe", "Gombe", "Gombe", 10.2897, 11.171),
		new Place("imo", "Owerri", "Imo", 5.484, 7.0351),
		new Place("jigawa", "Dutse", "Jigawa", 11.7594, 9.3392),
		new Place("kaduna", "Kaduna", "Kaduna", 10.5105, 7.4165),
		new Place("kano", "Kano", "Kano", 12.0022, 8.592),
		new Place("katsina", "Katsina", "Katsina", 12.9908, 7.6018),
		new Place("kebbi", "Birnin Kebbi", "Kebbi", 12.4539, 4.1975),
		new Place("kogi", "Lokoja", "Kogi", 7.8023, 6.7333),
		new Place("kwara", "Ilorin", "Kwara", 8.4799, 4.5418),
		new Place("lagos", "Lagos", "Lagos", 6.5244, 3.3792),
		new Place("nasarawa", "Lafia", "Nasarawa", 8.4933, 8.5155),
		new Place("niger", "Minna", "Niger", 9.6139, 6.5569),
		new Place("ogun", "Abeokuta", "Ogun", 7.1475, 3.3619),
		new Place("ondo", "Akure", "Ondo", 7.2526, 5.1931),
		new Place("osun", "Osogbo", "Osun", 7.7827, 4.5418),
		new Place("oyo", "Ibadan", "Oyo", 7.3775, 3.947),
		new Place("plateau", "Jos", "Plateau", 9.8965, 8.8583),
		new Place("rivers", "Port Harcourt", "Rivers", 4.8156, 7.0498),
		new Place("sokoto", "Sokoto", "Sokoto", 13.0059, 5.2476),
		new Place("taraba", "Jalingo", "Taraba", 8.8932, 11.3594),
		new Place("yobe", "Damaturu", "Yobe", 11.7469, 11.9608),
		new Place("zamfara", "Gusau", "Zamfara", 12.1704, 6.6641)
	};

	public static Place? FindById(string? id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return null;
		}
		return Places.FirstOrDefault((Place p) => string.Equals(p.Id, id.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(p.State, id.Trim(), StringComparison.OrdinalIgnoreCase));
	}

	public static Place Nearest(double lat, double lon)
	{
		return Places.OrderBy((Place p) => HaversineKm(lat, lon, p.Lat, p.Lon)).First();
	}

	public static string FormatLabel(Place place)
	{
		return place.Name + ", " + place.State;
	}

	private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
	{
		double num = (lat2 - lat1) * Math.PI / 180.0;
		double num2 = (lon2 - lon1) * Math.PI / 180.0;
		double num3 = Math.Sin(num / 2.0) * Math.Sin(num / 2.0) + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(num2 / 2.0) * Math.Sin(num2 / 2.0);
		return 12742.0 * Math.Atan2(Math.Sqrt(num3), Math.Sqrt(1.0 - num3));
	}
}
