namespace EcewsPortal.Models;

public sealed class DirectoryUser
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public bool AccountEnabled { get; set; } = true;
    /// <summary>signin = captured from portal login; directory = Microsoft Graph</summary>
    public string Source { get; set; } = "signin";
}

public sealed class UserAppAssignmentPageModel
{
    public IReadOnlyList<DirectoryUser> Users { get; set; } = [];
    public IReadOnlyList<AppItem> AllApps { get; set; } = [];
    public Dictionary<string, HashSet<int>> Assignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? SelectedEmail { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string UserSourceLabel { get; set; } = "Signed-in users";
}
