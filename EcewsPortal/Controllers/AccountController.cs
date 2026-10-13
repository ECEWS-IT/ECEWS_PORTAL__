using System.Security.Claims;
using EcewsPortal.Models;
using EcewsPortal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcewsPortal.Controllers;

public class AccountController(
    IActiveUserTracker activeUsers,
    IStaffEmailAllowlistStore allowlist,
    IPortalUserStore portalUsers,
    IAnalyticsStore analytics,
    ILogger<AccountController> logger) : Controller
{
    /// <summary>Microsoft Entra / directory sign-in.</summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignIn(string? redirectUri = null)
    {
        var redirect = string.IsNullOrWhiteSpace(redirectUri) ? Url.Content("~/")! : redirectUri;
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirect },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>Chooser: staff email (no OTP) or Microsoft directory login.</summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? redirectUri = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(SafeRedirect(redirectUri));

        return View(new EmailLoginViewModel { RedirectUri = redirectUri });
    }

    /// <summary>Staff email login — allowlisted emails open the portal immediately (no code).</summary>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailSignIn(EmailLoginViewModel model)
    {
        model.Email = (model.Email ?? string.Empty).Trim();
        if (!ModelState.IsValid)
            return View("Login", model);

        if (!allowlist.IsAllowed(model.Email))
        {
            model.ErrorMessage =
                "This email is not authorised for portal access. Ask an administrator to add it on Admin Settings.";
            return View("Login", model);
        }

        await IssueCookieAsync(model.Email);
        return LocalRedirect(SafeRedirect(model.RedirectUri));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SignOutUser()
    {
        activeUsers.Remove(User);
        return SignOut(
            new AuthenticationProperties { RedirectUri = Url.Content("~/") },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task IssueCookieAsync(string email)
    {
        var displayName = email.Split('@')[0];
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Upn, email),
            new("preferred_username", email),
            new("name", displayName),
            new("auth_method", "email")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        activeUsers.ClearRevocation(email);
        activeUsers.Touch(principal, HttpContext, isNewSignIn: true);
        _ = analytics.RecordLoginAsync(principal, HttpContext);
        try
        {
            portalUsers.Upsert(email, displayName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not persist portal user after email login.");
        }
    }

    private string SafeRedirect(string? redirectUri)
    {
        if (!string.IsNullOrWhiteSpace(redirectUri) && Url.IsLocalUrl(redirectUri))
            return redirectUri;
        return Url.Content("~/")!;
    }
}
