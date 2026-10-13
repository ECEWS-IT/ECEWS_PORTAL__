using EcewsPortal.Models;

namespace EcewsPortal.Services;

public class PortalService(
    IAppCatalogStore appCatalog,
    IUserAppAssignmentStore assignments) : IPortalService
{
    private static readonly string[] ColorCycle = ["aig", "aib", "ait", "aip", "aia", "aic", "aik", "air"];
    private static readonly string[] IconCycle = ["ti-apps", "ti-world", "ti-briefcase", "ti-device-desktop", "ti-cloud", "ti-database", "ti-chart-bar", "ti-settings"];

    private static readonly AppItem[] BuiltInApps =
    [
        new() { Id = 1, Name = "Official Website", Tag = "Public portal", IconClass = "ti-world", ColorClass = "aig", Categories = ["work"], Url = "https://ecews.org" },
        new() { Id = 2, Name = "Timesheet", Tag = "HR · Attendance", IconClass = "ti-clock", ColorClass = "aib", Categories = ["work"] },
        new() { Id = 3, Name = "Travels", Tag = "Logistics", IconClass = "ti-plane", ColorClass = "ait", Categories = ["work"] },
        new() { Id = 4, Name = "Careers", Tag = "Recruitment", IconClass = "ti-users", ColorClass = "aip", Categories = ["work"] },
        new() { Id = 5, Name = "Easy Profiler", Tag = "Staff profiles", IconClass = "ti-user-check", ColorClass = "aia", Categories = ["work"] },
        new() { Id = 6, Name = "Knowledge Mgmt", Tag = "Learning portal", IconClass = "ti-bulb", ColorClass = "aig", Categories = ["work"], Url = "https://ecews.sharepoint.com/sites/kms", IsExternal = true },
        new() { Id = 7, Name = "ADL", Tag = "Health platform", IconClass = "ti-heart", ColorClass = "aic", Categories = ["health"] },
        new() { Id = 8, Name = "ECEWS Intranet", Tag = "SharePoint", IconClass = "ti-brand-windows", ColorClass = "aib", Categories = ["work"], Url = "https://ecews.sharepoint.com", IsExternal = true },
        new() { Id = 9, Name = "SAGE 300 Finance", Tag = "Accounting", IconClass = "ti-report-money", ColorClass = "aia", Categories = ["finance"] },
        new() { Id = 10, Name = "Sage 300 People", Tag = "HR system", IconClass = "ti-briefcase", ColorClass = "aig", Categories = ["finance"] },
        new() { Id = 11, Name = "My Office Tools", Tag = "Microsoft 365", IconClass = "ti-apps", ColorClass = "aia", Categories = ["work"] },
        // Same-origin launch mints a ticket; HireDesk still requires email OTP.
        new()
        {
            Id = 12,
            Name = "HireDesk",
            Tag = "Interview & hiring",
            IconClass = "ti-clipboard-list",
            ColorClass = "aip",
            Categories = ["work"],
            Url = "/apps/hiredesk/launch",
            IsExternal = false,
        },
    ];

    public IReadOnlyList<AppItem> GetAllApps()
    {
        var overrides = appCatalog.GetOverrides();
        var removed = appCatalog.GetRemovedIds();

        var builtIn = BuiltInApps
            .Where(a => !removed.Contains(a.Id))
            .Select(a =>
            {
                var copy = new AppItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    Tag = a.Tag,
                    Description = a.Description,
                    IconClass = a.IconClass,
                    ColorClass = a.ColorClass,
                    Categories = a.Categories,
                    Url = a.Url,
                    LogoUrl = a.LogoUrl,
                    IsExternal = a.IsExternal,
                    IsCustom = false,
                    Visibility = AppVisibility.All,
                    AllowedEmails = []
                };

                if (overrides.TryGetValue(a.Id, out var o))
                {
                    if (!string.IsNullOrWhiteSpace(o.Name))
                        copy.Name = o.Name;
                    if (!string.IsNullOrWhiteSpace(o.Url))
                    {
                        copy.Url = o.Url;
                        copy.IsExternal = true;
                    }
                    if (!string.IsNullOrWhiteSpace(o.Description))
                    {
                        copy.Description = o.Description;
                        copy.Tag = o.Description;
                    }
                    else if (!string.IsNullOrWhiteSpace(o.Tag))
                    {
                        copy.Tag = o.Tag;
                        copy.Description = o.Tag;
                    }
                    if (!string.IsNullOrWhiteSpace(o.LogoUrl))
                        copy.LogoUrl = o.LogoUrl;

                    copy.Visibility = AppVisibility.IsSelected(o.Visibility)
                        ? AppVisibility.Selected
                        : AppVisibility.All;
                    copy.AllowedEmails = AppCatalogStore.NormalizeEmails(o.AllowedEmails);
                }

                if (string.IsNullOrWhiteSpace(copy.Description))
                    copy.Description = copy.Tag;

                return copy;
            });

        var custom = appCatalog.GetCustomApps().Select(a =>
        {
            if (string.IsNullOrWhiteSpace(a.Description))
                a.Description = a.Tag;
            a.Visibility = AppVisibility.IsSelected(a.Visibility)
                ? AppVisibility.Selected
                : AppVisibility.All;
            a.AllowedEmails = AppCatalogStore.NormalizeEmails(a.AllowedEmails);
            return a;
        });

        return builtIn.Concat(custom).ToList();
    }

    public IReadOnlyList<AppItem> GetAppsForUser(string? userEmail, bool isAdmin)
    {
        var email = (userEmail ?? string.Empty).Trim();
        var all = GetAllApps().Where(a => CanUserSeeApp(a, email)).ToList();
        var ids = assignments.GetAssignedAppIds(email);

        // Explicit per-user assignments further narrow the app-level visibility.
        if (assignments.HasAnyAssignments(email))
            return all.Where(a => ids.Contains(a.Id)).ToList();

        return all;
    }

    private static bool CanUserSeeApp(AppItem app, string userEmail)
    {
        if (!AppVisibility.IsSelected(app.Visibility))
            return true;

        if (string.IsNullOrWhiteSpace(userEmail))
            return false;

        return app.AllowedEmails.Any(e =>
            string.Equals(e, userEmail, StringComparison.OrdinalIgnoreCase));
    }

    public PortalViewModel GetPortalData(string? userEmail = null, bool isAdmin = false)
    {
        var now = DateTime.Now;
        var hour = now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";

        return new PortalViewModel
        {
            Greeting = greeting,
            TodayDate = now.ToString("dddd, MMMM d, yyyy"),
            StaffName = "John Doe",
            StaffInitials = "JD",
            NotificationCount = 0,
            Categories =
            [
                new NavCategory { Id = "all", Label = "Home", IconClass = "ti-home", IsActive = true },
                new NavCategory { Id = "work", Label = "Work", IconClass = "ti-briefcase" },
                new NavCategory { Id = "finance", Label = "Finance", IconClass = "ti-report-money" },
                new NavCategory { Id = "health", Label = "Health", IconClass = "ti-heartbeat" },
                new NavCategory { Id = "reports", Label = "Reports", IconClass = "ti-chart-bar" },
                new NavCategory { Id = "work", Label = "Knowledge", IconClass = "ti-books" },
                new NavCategory { Id = "work", Label = "Tools", IconClass = "ti-tool" },
            ],
            Applications = GetAppsForUser(userEmail, isAdmin),
            HeroArticles =
            [
                new HeroArticle
                {
                    Source = "ECEWS Rising · May–June 2026",
                    Title = "Great Is Thy Faithfulness — celebrating 25 years of ECEWS at our Silver Jubilee",
                    Emoji = "🎉",
                    SizeClass = "hero-main",
                    Badge = "Anniversary Edition",
                    ArticleSlug = "silver-jubilee",
                    Url = "/Home/Article/silver-jubilee",
                    ImageUrl = "/newsletters/clips/cover-1-jubilee.jpg"
                },
                new HeroArticle
                {
                    Source = "Cover Story · Newsletter",
                    Title = "ECEWS leads Lenacapavir PrEP rollout in Ebonyi, Akwa Ibom & Cross River States",
                    Emoji = "💉",
                    SizeClass = "hero-sm1",
                    ArticleSlug = "lenacapavir-prep",
                    Url = "/Home/Article/lenacapavir-prep",
                    ImageUrl = "/newsletters/clips/cover-2-prep.jpg"
                },
                new HeroArticle
                {
                    Source = "Infrastructure · Newsletter",
                    Title = "100KVA solar power system commissioned at NIMR to strengthen research and care",
                    Emoji = "☀️",
                    SizeClass = "hero-sm2",
                    ArticleSlug = "nimr-solar",
                    Url = "/Home/Article/nimr-solar",
                    ImageUrl = "/newsletters/clips/cover-3-solar.jpg"
                },
                new HeroArticle
                {
                    Source = "Programmes · ECEWS Rising",
                    Title = "Stronger together — programme teams and partners driving impact across Nigeria",
                    Emoji = "🤝",
                    SizeClass = "hero-main",
                    Url = "/newsletters/latest.pdf",
                    ImageUrl = "/newsletters/clips/cover-4-programmes.jpg"
                },
                new HeroArticle
                {
                    Source = "Community Health · Newsletter",
                    Title = "Reaching every community — frontline health workers bringing care closer to home",
                    Emoji = "🏥",
                    SizeClass = "hero-main",
                    Url = "/newsletters/latest.pdf",
                    ImageUrl = "/newsletters/clips/cover-5-outreach.jpg"
                },
            ],
            QuickLinks =
            [
                new QuickLink { Name = "SharePoint Intranet", Description = "ECEWS internal portal", IconClass = "ti-brand-windows", ColorClass = "aib", Url = "https://ecews.sharepoint.com", IsExternal = true },
                new QuickLink { Name = "Knowledge Mgmt System", Description = "KMS · SharePoint site", IconClass = "ti-books", ColorClass = "aig", Url = "https://ecews.sharepoint.com/sites/kms", IsExternal = true },
                new QuickLink { Name = "IT Help Desk", Description = "ECEWS support portal", IconClass = "ti-headset", ColorClass = "aia", Url = "https://helpdesk.ecews.org", IsExternal = true },
                new QuickLink { Name = "IT Support Tickets", Description = "Log & track requests", IconClass = "ti-tool", ColorClass = "air", Url = "https://ecews.sharepoint.com/sites/itsupport", IsExternal = true },
                new QuickLink { Name = "Submit Timesheet", Description = "Due: end of month", IconClass = "ti-clock", ColorClass = "ait" },
                new QuickLink { Name = "Request Travel", Description = "Travels portal", IconClass = "ti-plane-departure", ColorClass = "aip" },
            ],
            News = [],
            TickerItems =
            [
                new TickerItem { Text = "All services operational" },
                new TickerItem { Text = "Q1 programme reports under review", Highlight = true },
                new TickerItem { Text = "Next maintenance: Sunday 02:00–04:00 WAT" },
                new TickerItem { Text = "17 pending timesheet approvals", Highlight = true },
            ],
            Weather = new WeatherInfo
            {
                Location = "Lagos, Nigeria",
                Temperature = "30°C",
                Description = "Partly cloudy · Humidity 78%",
                Icon = "⛅",
                Forecast =
                [
                    new DailyForecast { Day = "Mon", Icon = "⛅", High = "32°", Low = "24°" },
                    new DailyForecast { Day = "Tue", Icon = "🌧", High = "29°", Low = "23°" },
                    new DailyForecast { Day = "Wed", Icon = "🌦", High = "31°", Low = "24°" },
                    new DailyForecast { Day = "Thu", Icon = "☀️", High = "33°", Low = "25°" },
                    new DailyForecast { Day = "Fri", Icon = "⛅", High = "31°", Low = "24°" },
                ]
            }
        };
    }

    public IReadOnlyList<AppItem> SearchApps(string? query, string? category, string? userEmail = null, bool isAdmin = false)
    {
        var results = GetAppsForUser(userEmail, isAdmin).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category) && category != "all")
            results = results.Where(a => a.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            results = results.Where(a =>
                a.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Tag.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return results.ToList();
    }

    public static string NextColorClass(int index) => ColorCycle[index % ColorCycle.Length];
    public static string NextIconClass(int index) => IconCycle[index % IconCycle.Length];
}
