namespace EcewsPortal.Models;

public class PortalViewModel
{
    public string Greeting { get; set; } = string.Empty;
    public string TodayDate { get; set; } = string.Empty;
    public IReadOnlyList<NavCategory> Categories { get; set; } = [];
    public IReadOnlyList<AppItem> Applications { get; set; } = [];
    public IReadOnlyList<HeroArticle> HeroArticles { get; set; } = [];
    public IReadOnlyList<NewsItem> News { get; set; } = [];
    public IReadOnlyList<QuickLink> QuickLinks { get; set; } = [];
    public IReadOnlyList<StatItem> Stats { get; set; } = [];
    public IReadOnlyList<TickerItem> TickerItems { get; set; } = [];
    public WeatherInfo Weather { get; set; } = new();
    public Announcement? Announcement { get; set; }
    public int NotificationCount { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffInitials { get; set; } = string.Empty;
    public string StaffEmail { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public IReadOnlyList<SidebarItem> SidebarItems { get; set; } = [];
}

public class NavCategory
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class SidebarItem
{
    public string Label { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public int? Badge { get; set; }
}

public static class AppVisibility
{
    public const string All = "all";
    public const string Selected = "selected";

    public static bool IsSelected(string? visibility) =>
        string.Equals(visibility, Selected, StringComparison.OrdinalIgnoreCase);
}

public class AppItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string ColorClass { get; set; } = string.Empty;
    public string[] Categories { get; set; } = [];
    public string? Url { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsExternal { get; set; }
    public bool IsCustom { get; set; }
    /// <summary>Who can see this app: <see cref="AppVisibility.All"/> or <see cref="AppVisibility.Selected"/>.</summary>
    public string Visibility { get; set; } = AppVisibility.All;
    /// <summary>Emails allowed when <see cref="Visibility"/> is selected.</summary>
    public List<string> AllowedEmails { get; set; } = [];
}

public class HeroArticle
{
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public string SizeClass { get; set; } = string.Empty;
    public string? Badge { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string? ArticleSlug { get; set; }
}

public class NewsItem
{
    public int Rank { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Url { get; set; }
}

public class PortalNotification
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string IconClass { get; set; } = "ti-bell";
}

public class ProfileViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string? JobHint { get; set; }
    public DateTimeOffset? FirstSeenAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public int SignInCount { get; set; }
    public int AssignedAppCount { get; set; }
}

public class NotificationsPageModel
{
    public IReadOnlyList<PortalNotification> Items { get; set; } = [];
    public int UnreadCount { get; set; }
}

public class WeatherPageModel
{
    public WeatherInfo Weather { get; set; } = new();
    public IReadOnlyList<WeatherPlaceOption> Places { get; set; } = [];
}

public class WeatherPlaceOption
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class QuickLink
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string ColorClass { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsExternal { get; set; }
}

public class StatItem
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string ColorClass { get; set; } = string.Empty;
    public string ActionText { get; set; } = string.Empty;
    public bool AnimateNumber { get; set; }
    public int? NumericValue { get; set; }
}

public class TickerItem
{
    public string Text { get; set; } = string.Empty;
    public bool Highlight { get; set; }
}

public class WeatherInfo
{
    public string Location { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public IReadOnlyList<DailyForecast> Forecast { get; set; } = [];
}

public class DailyForecast
{
    public string Day { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string High { get; set; } = string.Empty;
    public string Low { get; set; } = string.Empty;
}

public class Announcement
{
    public string Message { get; set; } = string.Empty;
    public string LinkText { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
}
