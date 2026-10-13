using System.Security.Claims;
using EcewsPortal.Models;
using EcewsPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EcewsPortal.Controllers;

/// <summary>Trusted launches into sibling ECEWS apps.</summary>
[Authorize]
public class AppsController(
    IOptions<HireDeskOptions> hireDeskOptions,
    IPortalService portalService,
    ILogger<AppsController> logger) : Controller
{
    private const int HireDeskAppId = 12;

    /// <summary>
    /// Open HireDesk with a signed portal ticket. HireDesk emails an OTP;
    /// the staff member must enter that code before a HireDesk session is created.
    /// </summary>
    [HttpGet("/apps/hiredesk/launch")]
    public IActionResult LaunchHireDesk()
    {
        var opts = hireDeskOptions.Value;
        if (string.IsNullOrWhiteSpace(opts.LaunchSecret))
        {
            logger.LogWarning("HireDesk launch attempted but LaunchSecret is empty.");
            return BadRequest("HireDesk portal launch is not configured. Ask an administrator.");
        }

        var email = ResolveEmail();
        if (string.IsNullOrWhiteSpace(email))
            return Challenge();

        if (!portalService.GetAppsForUser(email, isAdmin: false).Any(a => a.Id == HireDeskAppId))
            return Forbid();

        string ticket;
        try
        {
            ticket = HireDeskLaunchTicket.Create(email, opts.LaunchSecret);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mint HireDesk launch ticket for {Email}", email);
            return StatusCode(500, "Could not start HireDesk launch.");
        }

        var loginBase = string.IsNullOrWhiteSpace(opts.LoginUrl)
            ? "https://hiredesk.ecews.org/login"
            : opts.LoginUrl.Trim();
        var sep = loginBase.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var target = $"{loginBase}{sep}portal_ticket={Uri.EscapeDataString(ticket)}";
        return Redirect(target);
    }

    private string ResolveEmail()
    {
        return User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.Upn)
            ?? string.Empty;
    }
}
