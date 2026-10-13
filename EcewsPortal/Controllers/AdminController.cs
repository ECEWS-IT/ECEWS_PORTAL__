using System.Globalization;
using System.Text;
using EcewsPortal.Models;
using EcewsPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EcewsPortal.Controllers;

[Authorize(Policy = AdminAccess.PolicyName)]
public class AdminController(
    IAppCatalogStore appCatalog,
    IPortalService portalService,
    IActiveUserTracker activeUsers,
    IAnalyticsStore analytics,
    IDirectoryUserService directoryUsers,
    IPortalUserStore portalUsers,
    IUserAppAssignmentStore assignments,
    IAdminRoleStore adminRoles,
    IStaffEmailAllowlistStore staffAllowlist,
    IOptions<AdminOptions> adminOptions,
    IWebHostEnvironment env) : Controller
{
    private static readonly string[] AllowedLogoExt = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"];

    [HttpGet]
    [Authorize(Policy = AdminPolicies.Applications)]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        return View(await BuildViewModelAsync(ct));
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicies.ActiveUsers)]
    public IActionResult ActiveUsers()
    {
        return View(new ActiveUsersViewModel
        {
            Snapshot = activeUsers.GetSnapshot(),
            IdleTimeout = activeUsers.IdleTimeout
        });
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicies.Analytics)]
    public IActionResult Analytics()
    {
        return View(BuildAnalytics());
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicies.StaffAccess)]
    public async Task<IActionResult> Users(string? email, CancellationToken ct)
    {
        // Always capture the current admin so they appear in the list immediately
        portalUsers.UpsertFromPrincipal(User);

        var signedIn = portalUsers.GetUsers();
        var (directory, graphError) = await directoryUsers.GetUsersAsync(ct);

        // Prefer signed-in users while Graph consent is pending; merge directory when available
        var byEmail = new Dictionary<string, DirectoryUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in signedIn)
            byEmail[u.Email] = u;
        foreach (var u in directory)
        {
            if (byEmail.TryGetValue(u.Email, out var existing))
            {
                existing.DisplayName = u.DisplayName;
                existing.JobTitle = u.JobTitle ?? existing.JobTitle;
                existing.Department = u.Department ?? existing.Department;
                existing.AccountEnabled = u.AccountEnabled;
                existing.Source = "directory";
            }
            else
            {
                u.Source = "directory";
                byEmail[u.Email] = u;
            }
        }

        // Include any emails that already have assignments but have not signed in yet
        foreach (var key in assignments.GetAllAssignments().Keys)
        {
            if (byEmail.ContainsKey(key))
                continue;
            byEmail[key] = new DirectoryUser
            {
                Id = key,
                Email = key,
                DisplayName = key,
                JobTitle = "Assignment only",
                Source = "signin",
                AccountEnabled = true
            };
        }

        var users = byEmail.Values
            .OrderByDescending(u => u.Source == "signin")
            .ThenBy(u => u.DisplayName)
            .ToList();

        var selected = email?.Trim();
        if (string.IsNullOrWhiteSpace(selected) && users.Count > 0)
            selected = users[0].Email;

        string? info = null;
        if (!string.IsNullOrEmpty(graphError) && signedIn.Count > 0)
        {
            info = "Showing users who have signed into the portal. Full Active Directory list will appear after Graph admin consent is granted.";
        }

        return View(new UserAppAssignmentPageModel
        {
            Users = users,
            AllApps = portalService.GetAllApps(),
            Assignments = assignments.GetAllAssignments()
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            SelectedEmail = selected,
            ErrorMessage = TempData["ErrorMessage"] as string
                ?? (signedIn.Count == 0 && !string.IsNullOrEmpty(graphError) ? graphError : null),
            SuccessMessage = TempData["SuccessMessage"] as string ?? info,
            UserSourceLabel = directory.Count > 0 ? "Directory + signed-in users" : "Signed-in users"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.StaffAccess)]
    public IActionResult SaveUserApps(string email, int[]? appIds)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.EndsWith("@ecews.org", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "A valid @ecews.org email is required.";
            return RedirectToAction(nameof(Users));
        }

        assignments.SetAssignments(email.Trim(), appIds ?? []);
        TempData["SuccessMessage"] = $"Apps updated for {email.Trim()}.";
        return RedirectToAction(nameof(Users), new { email = email.Trim() });
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicies.SuperAdmin)]
    public IActionResult Administrators()
    {
        return View(BuildAdminManagementViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.SuperAdmin)]
    public IActionResult SaveAdministrator(string email, string[]? pages)
    {
        email = email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email)
            || !email.EndsWith("@ecews.org", StringComparison.OrdinalIgnoreCase))
        {
            TempData["AdminError"] = "Enter a valid @ecews.org email address.";
            return RedirectToAction(nameof(Administrators));
        }

        var superAdmin = AdminAccess.GetSuperAdminEmail(adminOptions.Value);
        if (string.Equals(email, superAdmin, StringComparison.OrdinalIgnoreCase))
        {
            TempData["AdminError"] = "The super-admin account is configured in appsettings and cannot be delegated.";
            return RedirectToAction(nameof(Administrators));
        }

        var allowedPages = (pages ?? [])
            .Where(page => AdminPages.All.Contains(page, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allowedPages.Length == 0)
        {
            TempData["AdminError"] = "Select at least one page for this administrator.";
            return RedirectToAction(nameof(Administrators));
        }

        adminRoles.Set(email, allowedPages);
        TempData["AdminSuccess"] = $"Administrator access updated for {email}.";
        return RedirectToAction(nameof(Administrators));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.SuperAdmin)]
    public IActionResult RemoveAdministrator(string email)
    {
        email = email?.Trim() ?? string.Empty;
        var superAdmin = AdminAccess.GetSuperAdminEmail(adminOptions.Value);
        if (string.Equals(email, superAdmin, StringComparison.OrdinalIgnoreCase))
        {
            TempData["AdminError"] = "The super-admin cannot be removed.";
            return RedirectToAction(nameof(Administrators));
        }

        var removed = adminRoles.Remove(email);
        TempData[removed ? "AdminSuccess" : "AdminError"] = removed
            ? $"Administrator access removed for {email}."
            : "Administrator was not found.";
        return RedirectToAction(nameof(Administrators));
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicies.Analytics)]
    public IActionResult AnalyticsData()
    {
        return Json(BuildAnalytics());
    }

    private AnalyticsDashboardDto BuildAnalytics() =>
        analytics.GetDashboard(portalService.GetAllApps(), activeUsers.GetActiveUsers().Count);

    [HttpGet]
    [Authorize(Policy = AdminPolicies.ActiveUsers)]
    public IActionResult ActiveUsersData()
    {
        return Json(activeUsers.GetSnapshot());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.ActiveUsers)]
    public IActionResult ForceSignOut([FromForm] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { ok = false, error = "Email is required." });

        activeUsers.ForceSignOut(email.Trim());
        return Json(new { ok = true, email = email.Trim() });
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicies.ActiveUsers)]
    public IActionResult ExportActiveUsersCsv()
    {
        var snap = activeUsers.GetSnapshot();
        var sb = new StringBuilder();
        sb.AppendLine("DisplayName,Email,Status,SignedInAtUtc,LastSeenAtUtc,Device,IpAddress,MultiIp");
        foreach (var u in snap.Users)
        {
            sb.AppendLine(string.Join(',',
                Csv(u.DisplayName),
                Csv(u.Email),
                Csv(u.ActivityStatus),
                Csv(u.SignedInAt.ToString("o", CultureInfo.InvariantCulture)),
                Csv(u.LastSeenAt.ToString("o", CultureInfo.InvariantCulture)),
                Csv(u.DeviceLabel),
                Csv(u.IpAddress ?? ""),
                u.MultiIp ? "yes" : "no"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"ecews-active-users-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicies.ActiveUsers)]
    public IActionResult ExportHistoryCsv()
    {
        var history = activeUsers.GetHistory(500);
        var sb = new StringBuilder();
        sb.AppendLine("DisplayName,Email,SignedInAtUtc,Device,IpAddress");
        foreach (var h in history)
        {
            sb.AppendLine(string.Join(',',
                Csv(h.DisplayName),
                Csv(h.Email),
                Csv(h.SignedInAt.ToString("o", CultureInfo.InvariantCulture)),
                Csv(h.DeviceLabel),
                Csv(h.IpAddress ?? "")));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"ecews-signin-history-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.Applications)]
    public IActionResult SaveStaffLoginEmails(string staffLoginEmailsText)
    {
        try
        {
            staffAllowlist.SaveEmailsText(staffLoginEmailsText);
            var count = staffAllowlist.GetEmails().Count;
            TempData["SuccessMessage"] = count == 0
                ? "Staff email allowlist cleared. No one can sign in with email until you add addresses."
                : $"Saved {count} staff email{(count == 1 ? "" : "s")} for email login.";
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Could not save the staff email allowlist.";
        }

        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.Applications)]
    public async Task<IActionResult> Settings(AppFormModel form, CancellationToken ct)
    {
        var model = await BuildViewModelAsync(ct);
        model.Form = form;

        if (string.IsNullOrWhiteSpace(form.Name))
        {
            model.ErrorMessage = "App name is required.";
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(form.Url))
        {
            model.ErrorMessage = "App URL is required.";
            return View(model);
        }

        if (!TryParseHttpUrl(form.Url, out var uri, out var urlError))
        {
            model.ErrorMessage = urlError;
            return View(model);
        }

        var visibility = ResolveVisibility(form.Visibility, form.AllowedEmails, form.ExtraEmailsText, out var allowedEmails, out var visibilityError);
        if (visibilityError is not null)
        {
            model.ErrorMessage = visibilityError;
            return View(model);
        }

        string? logoUrl = null;
        if (form.Logo is { Length: > 0 })
        {
            var (ok, path, error) = await SaveLogoAsync(form.Logo);
            if (!ok)
            {
                model.ErrorMessage = error;
                return View(model);
            }
            logoUrl = path;
        }

        var existingCount = appCatalog.GetCustomApps().Count;
        var description = (form.Description ?? string.Empty).Trim();

        appCatalog.AddApp(new AppItem
        {
            Name = form.Name.Trim(),
            Url = uri!.ToString(),
            Description = description,
            Tag = string.IsNullOrWhiteSpace(description) ? "Custom app" : description,
            LogoUrl = logoUrl,
            IconClass = PortalService.NextIconClass(existingCount),
            ColorClass = PortalService.NextColorClass(existingCount),
            Categories = ["work"],
            IsExternal = true,
            IsCustom = true,
            Visibility = visibility,
            AllowedEmails = allowedEmails
        });

        TempData["SuccessMessage"] = visibility == AppVisibility.Selected
            ? $"“{form.Name.Trim()}” was added for {allowedEmails.Count} selected user{(allowedEmails.Count == 1 ? "" : "s")}."
            : $"“{form.Name.Trim()}” was added and is visible to everyone.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.Applications)]
    public IActionResult UpdateApp(AppEditModel form)
    {
        var app = portalService.GetAllApps().FirstOrDefault(a => a.Id == form.Id);
        if (app is null)
        {
            TempData["ErrorMessage"] = "App not found.";
            return RedirectToAction(nameof(Settings));
        }

        if (string.IsNullOrWhiteSpace(form.Name))
        {
            TempData["ErrorMessage"] = "App name is required.";
            return RedirectToAction(nameof(Settings));
        }

        string? url = string.IsNullOrWhiteSpace(form.Url) ? null : form.Url.Trim();
        Uri? uri = null;
        if (!string.IsNullOrWhiteSpace(url) && !TryParseHttpUrl(url, out uri, out var urlError))
        {
            TempData["ErrorMessage"] = urlError;
            return RedirectToAction(nameof(Settings));
        }

        if (uri is not null)
            url = uri.ToString();

        var visibility = ResolveVisibility(form.Visibility, form.AllowedEmails, form.ExtraEmailsText, out var allowedEmails, out var visibilityError);
        if (visibilityError is not null)
        {
            TempData["ErrorMessage"] = visibilityError;
            return RedirectToAction(nameof(Settings));
        }

        var name = form.Name.Trim();
        var description = (form.Description ?? string.Empty).Trim();
        appCatalog.UpdateApp(form.Id, name, url, description, visibility: visibility, allowedEmails: allowedEmails);
        TempData["SuccessMessage"] = visibility == AppVisibility.Selected
            ? $"“{name}” was updated · visible to {allowedEmails.Count} selected user{(allowedEmails.Count == 1 ? "" : "s")}."
            : $"“{name}” was updated · visible to everyone.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.Applications)]
    public IActionResult DeleteApp(int id)
    {
        var app = portalService.GetAllApps().FirstOrDefault(a => a.Id == id);
        if (appCatalog.DeleteApp(id))
            TempData["SuccessMessage"] = app is null
                ? "App removed from the portal."
                : $"“{app.Name}” was removed from the portal.";
        else
            TempData["ErrorMessage"] = "Could not remove that app.";

        return RedirectToAction(nameof(Settings));
    }

    private async Task<AdminSettingsViewModel> BuildViewModelAsync(CancellationToken ct = default)
    {
        var emails = staffAllowlist.GetEmails();
        return new AdminSettingsViewModel
        {
            AllApps = portalService.GetAllApps(),
            StaffUsers = await LoadStaffUsersAsync(ct),
            StaffLoginEmailsText = staffAllowlist.GetEmailsText(),
            StaffLoginEmailCount = emails.Count,
            SuccessMessage = TempData["SuccessMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string
        };
    }

    private async Task<IReadOnlyList<DirectoryUser>> LoadStaffUsersAsync(CancellationToken ct)
    {
        portalUsers.UpsertFromPrincipal(User);
        var signedIn = portalUsers.GetUsers();
        var (directory, _) = await directoryUsers.GetUsersAsync(ct);

        var byEmail = new Dictionary<string, DirectoryUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in signedIn)
            byEmail[u.Email] = u;
        foreach (var u in directory)
        {
            if (byEmail.TryGetValue(u.Email, out var existing))
            {
                existing.DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? existing.DisplayName : u.DisplayName;
                existing.JobTitle = u.JobTitle ?? existing.JobTitle;
                existing.Department = u.Department ?? existing.Department;
                existing.AccountEnabled = u.AccountEnabled;
                existing.Source = "directory";
            }
            else
            {
                u.Source = "directory";
                byEmail[u.Email] = u;
            }
        }

        foreach (var key in assignments.GetAllAssignments().Keys)
        {
            if (byEmail.ContainsKey(key))
                continue;
            byEmail[key] = new DirectoryUser
            {
                Id = key,
                Email = key,
                DisplayName = key,
                Source = "signin",
                AccountEnabled = true
            };
        }

        return byEmail.Values
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveVisibility(
        string? visibility,
        string[]? allowedEmails,
        string? extraEmailsText,
        out List<string> emails,
        out string? error)
    {
        error = null;
        var selected = AppVisibility.IsSelected(visibility);
        var merged = new List<string>();
        if (allowedEmails is { Length: > 0 })
            merged.AddRange(allowedEmails);
        if (!string.IsNullOrWhiteSpace(extraEmailsText))
            merged.Add(extraEmailsText);

        emails = AppCatalogStore.NormalizeEmails(merged);
        if (!selected)
        {
            emails = [];
            return AppVisibility.All;
        }

        if (emails.Count == 0)
        {
            error = "Select at least one user, or switch visibility to Everyone.";
            return AppVisibility.Selected;
        }

        return AppVisibility.Selected;
    }

    private AdminManagementViewModel BuildAdminManagementViewModel()
    {
        var pageDescriptions = new Dictionary<string, (string Description, string Icon)>
        {
            [AdminPages.Applications] = ("Create, edit, and remove portal applications.", "ti-apps"),
            [AdminPages.StaffAccess] = ("Assign portal applications to staff accounts.", "ti-user-cog"),
            [AdminPages.ActiveUsers] = ("View sessions, exports, and force sign-out.", "ti-antenna"),
            [AdminPages.Analytics] = ("View portal usage and application analytics.", "ti-chart-area-line")
        };

        return new AdminManagementViewModel
        {
            SuperAdminEmail = AdminAccess.GetSuperAdminEmail(adminOptions.Value),
            Admins = adminRoles.GetAll().Select(a => new AdminAccountViewModel
            {
                Email = a.Email,
                Pages = new HashSet<string>(a.Pages, StringComparer.OrdinalIgnoreCase),
                UpdatedAt = a.UpdatedAt
            }).ToList(),
            AvailablePages = AdminPages.All.Select(page => new AdminPageOption
            {
                Key = page,
                Label = AdminPages.Label(page),
                Description = pageDescriptions[page].Description,
                Icon = pageDescriptions[page].Icon
            }).ToList(),
            SuccessMessage = TempData["AdminSuccess"] as string,
            ErrorMessage = TempData["AdminError"] as string
        };
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static bool TryParseHttpUrl(string value, out Uri? uri, out string error)
    {
        uri = null;
        error = string.Empty;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Enter a valid URL starting with http:// or https://.";
            uri = null;
            return false;
        }
        return true;
    }

    private async Task<(bool Ok, string? Path, string? Error)> SaveLogoAsync(IFormFile logo)
    {
        var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
        if (!AllowedLogoExt.Contains(ext))
            return (false, null, "Logo must be PNG, JPG, GIF, WEBP, or SVG.");

        if (logo.Length > 2 * 1024 * 1024)
            return (false, null, "Logo must be 2 MB or smaller.");

        var uploads = Path.Combine(env.WebRootPath, "uploads", "apps");
        Directory.CreateDirectory(uploads);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var savePath = Path.Combine(uploads, fileName);
        await using (var stream = System.IO.File.Create(savePath))
        {
            await logo.CopyToAsync(stream);
        }

        return (true, $"/uploads/apps/{fileName}", null);
    }
}
