using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public sealed class ActiveUserTracker : IActiveUserTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ConcurrentDictionary<string, ActiveUserSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revoked = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _historyLock = new();
    private readonly List<SignInHistoryEntry> _history = [];
    private readonly string _historyPath;
    private int _peakConcurrent;

    public TimeSpan IdleTimeout { get; } = TimeSpan.FromMinutes(20);
    public TimeSpan IdleWarnAfter { get; } = TimeSpan.FromMinutes(5);

    public ActiveUserTracker(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _historyPath = Path.Combine(dataDir, "signin-history.json");
        LoadHistory();
    }

    public void Touch(ClaimsPrincipal user, HttpContext httpContext, bool isNewSignIn = false)
    {
        var email = GetEmail(user);
        if (string.IsNullOrWhiteSpace(email))
            return;

        var now = DateTimeOffset.UtcNow;
        var displayName = GetDisplayName(user);
        var userId = GetUserId(user) ?? email;
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var ua = httpContext.Request.Headers.UserAgent.ToString();
        if (ua.Length > 240)
            ua = ua[..240];
        var device = DeviceLabelParser.Parse(ua);

        var created = false;
        _sessions.AddOrUpdate(
            email,
            _ =>
            {
                created = true;
                var session = new ActiveUserSession
                {
                    UserId = userId,
                    Email = email,
                    DisplayName = displayName,
                    SignedInAt = now,
                    LastSeenAt = now,
                    IpAddress = ip,
                    UserAgent = ua,
                    DeviceLabel = device
                };
                if (!string.IsNullOrWhiteSpace(ip))
                    session.IpAddresses.Add(ip);
                return session;
            },
            (_, existing) =>
            {
                existing.LastSeenAt = now;
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    existing.IpAddress = ip;
                    existing.IpAddresses.Add(ip);
                    existing.MultiIp = existing.IpAddresses.Count > 1;
                }
                if (!string.IsNullOrWhiteSpace(ua))
                {
                    existing.UserAgent = ua;
                    existing.DeviceLabel = device;
                }
                if (!string.IsNullOrWhiteSpace(displayName))
                    existing.DisplayName = displayName;
                return existing;
            });

        if (created || isNewSignIn)
            AppendHistory(new SignInHistoryEntry
            {
                Email = email,
                DisplayName = displayName,
                SignedInAt = now,
                IpAddress = ip,
                DeviceLabel = device
            });

        UpdatePeak();
    }

    public void Remove(ClaimsPrincipal user)
    {
        var email = GetEmail(user);
        if (!string.IsNullOrWhiteSpace(email))
            RemoveByEmail(email);
    }

    public void RemoveByEmail(string email)
    {
        if (!string.IsNullOrWhiteSpace(email))
            _sessions.TryRemove(email, out _);
    }

    public bool ForceSignOut(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        _revoked[email.Trim()] = DateTimeOffset.UtcNow;
        return _sessions.TryRemove(email.Trim(), out _) || true;
    }

    public bool IsRevoked(string email) =>
        !string.IsNullOrWhiteSpace(email) && _revoked.ContainsKey(email);

    public void ClearRevocation(string email)
    {
        if (!string.IsNullOrWhiteSpace(email))
            _revoked.TryRemove(email, out _);
    }

    public IReadOnlyList<ActiveUserSession> GetActiveUsers() => BuildActiveList();

    public ActiveUsersSnapshot GetSnapshot()
    {
        var users = BuildActiveList();
        var startOfUtcDay = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        List<SignInHistoryEntry> history;
        List<SignInHistoryEntry> todayEntries;
        lock (_historyLock)
        {
            history = _history
                .OrderByDescending(h => h.SignedInAt)
                .Take(100)
                .ToList();
            todayEntries = _history.Where(h => h.SignedInAt >= startOfUtcDay).ToList();
        }

        return new ActiveUsersSnapshot
        {
            Users = users,
            History = history,
            Stats = new ActiveUsersStats
            {
                ActiveNow = users.Count(u => u.ActivityStatus == "active"),
                IdleNow = users.Count(u => u.ActivityStatus == "idle"),
                SignInsToday = todayEntries.Count,
                UniqueUsersToday = todayEntries.Select(h => h.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                PeakConcurrent = Math.Max(_peakConcurrent, users.Count),
                MostRecentSignIn = users.Count > 0
                    ? users.Max(u => u.SignedInAt)
                    : history.FirstOrDefault()?.SignedInAt,
                MultiIpAlerts = users.Count(u => u.MultiIp)
            },
            IdleTimeoutMinutes = (int)IdleTimeout.TotalMinutes,
            IdleWarnMinutes = (int)IdleWarnAfter.TotalMinutes,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<SignInHistoryEntry> GetHistory(int take = 100)
    {
        lock (_historyLock)
        {
            return _history.OrderByDescending(h => h.SignedInAt).Take(take).ToList();
        }
    }

    private List<ActiveUserSession> BuildActiveList()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - IdleTimeout;
        var warn = now - IdleWarnAfter;

        foreach (var pair in _sessions)
        {
            if (pair.Value.LastSeenAt < cutoff)
                _sessions.TryRemove(pair.Key, out _);
        }

        return _sessions.Values
            .Where(s => s.LastSeenAt >= cutoff)
            .Select(s =>
            {
                s.ActivityStatus = s.LastSeenAt >= warn ? "active" : "idle";
                s.MultiIp = s.IpAddresses.Count > 1;
                return s;
            })
            .OrderByDescending(s => s.LastSeenAt)
            .ToList();
    }

    private void UpdatePeak()
    {
        var count = _sessions.Count;
        if (count > _peakConcurrent)
            _peakConcurrent = count;
    }

    private void AppendHistory(SignInHistoryEntry entry)
    {
        lock (_historyLock)
        {
            // Avoid duplicate rows within 2 minutes for same email (page refreshes after login)
            var recent = _history.LastOrDefault(h =>
                h.Email.Equals(entry.Email, StringComparison.OrdinalIgnoreCase));
            if (recent is not null && entry.SignedInAt - recent.SignedInAt < TimeSpan.FromMinutes(2))
                return;

            _history.Add(entry);
            if (_history.Count > 500)
                _history.RemoveRange(0, _history.Count - 500);
            SaveHistoryUnlocked();
        }
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath))
                return;
            var json = File.ReadAllText(_historyPath);
            var items = JsonSerializer.Deserialize<List<SignInHistoryEntry>>(json);
            if (items is { Count: > 0 })
            {
                lock (_historyLock)
                {
                    _history.Clear();
                    _history.AddRange(items.OrderBy(h => h.SignedInAt).TakeLast(500));
                }
            }
        }
        catch
        {
            // Ignore corrupt history; start fresh.
        }
    }

    private void SaveHistoryUnlocked()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, JsonOptions);
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    internal static string GetEmail(ClaimsPrincipal user) =>
        user.FindFirstValue("preferred_username")
        ?? user.FindFirstValue(ClaimTypes.Email)
        ?? user.FindFirstValue(ClaimTypes.Upn)
        ?? string.Empty;

    private static string GetDisplayName(ClaimsPrincipal user) =>
        user.FindFirstValue("name")
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? GetEmail(user);

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue("oid")
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
}

public static class DeviceLabelParser
{
    public static string Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Unknown";

        var ua = userAgent;
        var browser =
            ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge" :
            ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Chrome" :
            ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Safari" :
            ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox" :
            ua.Contains("MSIE", StringComparison.OrdinalIgnoreCase) || ua.Contains("Trident/", StringComparison.OrdinalIgnoreCase) ? "IE" :
            "Browser";

        var os =
            ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" :
            ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android" :
            ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS" :
            ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) || ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) ? "macOS" :
            ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux" :
            "Unknown OS";

        return $"{browser} on {os}";
    }
}
