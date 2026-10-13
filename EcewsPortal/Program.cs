using System.Security.Claims;
using EcewsPortal.Middleware;
using EcewsPortal.Models;
using EcewsPortal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<HireDeskOptions>(builder.Configuration.GetSection(HireDeskOptions.SectionName));
builder.Services.AddSingleton<IAdminRoleStore, AdminRoleStore>();
builder.Services.AddSingleton<IAdminAccessService, AdminAccessService>();
builder.Services.AddSingleton<IStaffEmailAllowlistStore, StaffEmailAllowlistStore>();
builder.Services.AddSingleton<IOtpService, OtpService>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

var keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("EcewsPortal");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Cookie scheme for email+OTP; OpenID Connect for Microsoft directory (existing) login.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    // ID token flow: no client secret required. Enable "ID tokens" in the
    // Entra app registration (Authentication → Implicit grant and hybrid flows).
    options.ResponseType = OpenIdConnectResponseType.IdToken;
    options.UsePkce = false;
    options.Prompt = "select_account";
    options.Events ??= new OpenIdConnectEvents();
    var existing = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        if (existing is not null)
            await existing(context);

        var email = context.Principal?.FindFirstValue("preferred_username")
            ?? context.Principal?.FindFirstValue(ClaimTypes.Email)
            ?? context.Principal?.FindFirstValue(ClaimTypes.Upn)
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(email) &&
            !email.EndsWith("@ecews.org", StringComparison.OrdinalIgnoreCase))
        {
            context.Fail("Only @ecews.org work accounts can sign in to the ECEWS Portal.");
            return;
        }

        if (context.Principal is not null)
        {
            var tracker = context.HttpContext.RequestServices.GetRequiredService<IActiveUserTracker>();
            if (!string.IsNullOrWhiteSpace(email))
                tracker.ClearRevocation(email);
            tracker.Touch(context.Principal, context.HttpContext, isNewSignIn: true);

            var analytics = context.HttpContext.RequestServices.GetRequiredService<IAnalyticsStore>();
            _ = analytics.RecordLoginAsync(context.Principal, context.HttpContext);

            try
            {
                var portalUsers = context.HttpContext.RequestServices.GetRequiredService<IPortalUserStore>();
                portalUsers.UpsertFromPrincipal(context.Principal);
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("PortalUserStore");
                logger.LogWarning(ex, "Could not persist portal user (check App_Data write permissions).");
            }
        }
    };

    var existingRemote = options.Events.OnRemoteFailure;
    options.Events.OnRemoteFailure = async context =>
    {
        if (existingRemote is not null)
            await existingRemote(context);

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("OpenIdConnect");
        logger.LogError(context.Failure, "Entra sign-in failed: {Message}", context.Failure?.Message);
        try
        {
            var logDir = Path.Combine(context.HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>().ContentRootPath, "logs");
            Directory.CreateDirectory(logDir);
            await File.AppendAllTextAsync(
                Path.Combine(logDir, "exceptions.log"),
                $"[{DateTimeOffset.Now:u}] OIDC: {context.Failure}{Environment.NewLine}");
        }
        catch { /* ignore log IO */ }

        context.Response.Redirect("/Account/AccessDenied");
        context.HandleResponse();
    };
});

builder.Services.AddSingleton<IAuthorizationHandler, AdminPermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy(AdminPolicies.AnyAdmin, policy =>
        policy.Requirements.Add(new AdminPermissionRequirement()));
    options.AddPolicy(AdminPolicies.SuperAdmin, policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(SuperAdminOnly: true)));
    options.AddPolicy(AdminPolicies.Applications, policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPages.Applications)));
    options.AddPolicy(AdminPolicies.StaffAccess, policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPages.StaffAccess)));
    options.AddPolicy(AdminPolicies.ActiveUsers, policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPages.ActiveUsers)));
    options.AddPolicy(AdminPolicies.Analytics, policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPages.Analytics)));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
    // Login chooser: staff email+OTP or Microsoft directory sign-in
    options.LoginPath = "/Account/Login";
    options.Events.OnRedirectToLogin = context =>
    {
        var redirect = context.RedirectUri;
        // Prefer our Login page (supports OTP + Microsoft) instead of immediate OIDC challenge
        context.Response.Redirect(redirect.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase)
            ? redirect
            : "/Account/Login?redirectUri=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        return Task.CompletedTask;
    };
});

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

builder.Services.Configure<NewsletterOptions>(builder.Configuration.GetSection(NewsletterOptions.SectionName));
builder.Services.AddSingleton<INewsletterPdfService, NewsletterPdfService>();
builder.Services.AddSingleton<IAppCatalogStore, AppCatalogStore>();
builder.Services.AddSingleton<IUserAppAssignmentStore, UserAppAssignmentStore>();
builder.Services.AddSingleton<IPortalUserStore, PortalUserStore>();
builder.Services.AddSingleton<IDirectoryUserService, DirectoryUserService>();
builder.Services.AddSingleton<IPortalService, PortalService>();
builder.Services.AddSingleton<IActiveUserTracker, ActiveUserTracker>();
builder.Services.AddSingleton<IGeoLocationService, GeoLocationService>();
builder.Services.AddSingleton<IAnalyticsStore, AnalyticsStore>();
builder.Services.AddSingleton<INewsletterArticleStore, NewsletterArticleStore>();
builder.Services.AddSingleton<INotificationStore, NotificationStore>();
builder.Services.AddWeatherService();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "logs"));

// The super-admin is configured in appsettings; delegated admins are stored in App_Data.
var superAdminEmail = builder.Configuration["Admin:SuperAdminEmail"]
    ?? builder.Configuration.GetSection("Admin:AdminEmails").Get<string[]>()?.FirstOrDefault();
if (string.IsNullOrWhiteSpace(superAdminEmail))
{
    app.Logger.LogWarning(
        "Admin:SuperAdminEmail is empty. Configure a super-admin email before using Admin pages.");
}

// Activate newsletter PDF watcher/publisher at startup
_ = app.Services.GetRequiredService<INewsletterPdfService>();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

