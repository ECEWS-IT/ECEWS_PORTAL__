using System.Security.Claims;
using System.Threading.Tasks;
using EcewsPortal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EcewsPortal.Middleware;

public sealed class ActiveUserTrackingMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context, IActiveUserTracker tracker)
	{
		if (context.User.Identity?.IsAuthenticated ?? false)
		{
			string email = context.User.FindFirstValue("preferred_username") ?? context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn") ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(email) && tracker.IsRevoked(email))
			{
				tracker.RemoveByEmail(email);
				await context.SignOutAsync("Cookies");
				await context.SignOutAsync("OpenIdConnect");
				context.Response.Redirect("/Account/SignIn");
				return;
			}
			tracker.Touch(context.User, context);
			context.RequestServices.GetRequiredService<IPortalUserStore>().UpsertFromPrincipal(context.User);
		}
		await next(context);
	}
}
