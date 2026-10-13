using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;
using Microsoft.AspNetCore.Http;

namespace EcewsPortal.Services;

public interface IAnalyticsStore
{
	Task RecordLoginAsync(ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct = default(CancellationToken));

	Task RecordAppVisitAsync(ClaimsPrincipal user, HttpContext httpContext, int appId, string appName, CancellationToken ct = default(CancellationToken));

	AnalyticsDashboardDto GetDashboard(IReadOnlyList<AppItem> catalogApps, int activeUsersNow);
}
