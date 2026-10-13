namespace EcewsPortal.Services;

public static class AdminPages
{
    public const string Applications = "applications";
    public const string StaffAccess = "staff-access";
    public const string ActiveUsers = "active-users";
    public const string Analytics = "analytics";

    public static readonly IReadOnlyList<string> All =
    [
        Applications,
        StaffAccess,
        ActiveUsers,
        Analytics
    ];

    public static string Label(string page) => page switch
    {
        Applications => "Applications",
        StaffAccess => "Users & app access",
        ActiveUsers => "Active users",
        Analytics => "Analytics",
        _ => page
    };
}
