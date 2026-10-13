using System.Security.Claims;
using System.Text.Json;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface IPortalUserStore
{
    void UpsertFromPrincipal(ClaimsPrincipal user);
    void Upsert(string email, string? displayName = null);
    IReadOnlyList<DirectoryUser> GetUsers();
    PortalUserStats? GetStats(string email);
}

public sealed class PortalUserStats
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public int SignInCount { get; set; }
}

public sealed class PortalUserStore : IPortalUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _lock = new();
    private readonly string _path;
    private Dictionary<string, PortalUserRecord> _map = new(StringComparer.OrdinalIgnoreCase);

    public PortalUserStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "portal-users.json");
        Load();
    }

    public void UpsertFromPrincipal(ClaimsPrincipal user)
    {
        var email = user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue(ClaimTypes.Upn)
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) ||
            !email.EndsWith("@ecews.org", StringComparison.OrdinalIgnoreCase))
            return;

        var name = user.FindFirstValue("name")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? email;
        Upsert(email, name);
    }

    public void Upsert(string email, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var key = email.Trim();
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                existing.LastSeenAt = DateTimeOffset.UtcNow;
                existing.SignInCount += 1;
                if (!string.IsNullOrWhiteSpace(displayName) &&
                    !displayName.Contains('@', StringComparison.Ordinal))
                    existing.DisplayName = displayName.Trim();
            }
            else
            {
                _map[key] = new PortalUserRecord
                {
                    Email = key,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim(),
                    FirstSeenAt = DateTimeOffset.UtcNow,
                    LastSeenAt = DateTimeOffset.UtcNow,
                    SignInCount = 1
                };
            }
            SaveUnlocked();
        }
    }

    public IReadOnlyList<DirectoryUser> GetUsers()
    {
        lock (_lock)
        {
            return _map.Values
                .OrderByDescending(u => u.LastSeenAt)
                .Select(u => new DirectoryUser
                {
                    Id = u.Email,
                    Email = u.Email,
                    DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName,
                    JobTitle = "Portal sign-in",
                    Department = $"Last seen {u.LastSeenAt.LocalDateTime:g}",
                    AccountEnabled = true,
                    Source = "signin"
                })
                .ToList();
        }
    }

    public PortalUserStats? GetStats(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        lock (_lock)
        {
            if (!_map.TryGetValue(email.Trim(), out var u))
                return null;

            return new PortalUserStats
            {
                Email = u.Email,
                DisplayName = u.DisplayName,
                FirstSeenAt = u.FirstSeenAt,
                LastSeenAt = u.LastSeenAt,
                SignInCount = u.SignInCount
            };
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            var json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<PortalUserRecord>>(json);
            if (list is null)
                return;
            _map = list
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .ToDictionary(u => u.Email.Trim(), u => u, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _map = new Dictionary<string, PortalUserRecord>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveUnlocked()
    {
        var list = _map.Values.OrderByDescending(u => u.LastSeenAt).ToList();
        File.WriteAllText(_path, JsonSerializer.Serialize(list, JsonOptions));
    }

    private sealed class PortalUserRecord
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTimeOffset FirstSeenAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public int SignInCount { get; set; }
    }
}
