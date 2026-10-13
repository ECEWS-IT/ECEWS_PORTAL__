using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface IGeoLocationService
{
	Task<GeoInfo> ResolveAsync(string? ipAddress, CancellationToken ct = default(CancellationToken));
}
