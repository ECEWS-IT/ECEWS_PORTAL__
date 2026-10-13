using System.Text.Json;

namespace EcewsPortal.Services;

public interface IStaffEmailAllowlistStore
{
    IReadOnlyList<string> GetEmails();
    string GetEmailsText();
    void SaveEmails(IEnumerable<string> emails);
    void SaveEmailsText(string? text);
    bool IsAllowed(string email);
}

/// <summary>
/// Admin-managed list of staff emails allowed to sign in with email + OTP.
/// Stored in App_Data/staff-login-emails.json
/// </summary>
public sealed class StaffEmailAllowlistStore(IWebHostEnvironment env) : IStaffEmailAllowlistStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path = Path.Combine(env.ContentRootPath, "App_Data", "staff-login-emails.json");

    public IReadOnlyList<string> GetEmails()
    {
        lock (_gate)
        {
            return LoadUnlocked()
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public string GetEmailsText() => string.Join(Environment.NewLine, GetEmails());

    public void SaveEmails(IEnumerable<string> emails)
    {
        var cleaned = Normalize(emails);
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(cleaned, JsonOptions));
        }
    }

    public void SaveEmailsText(string? text)
    {
        var lines = (text ?? string.Empty)
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        SaveEmails(lines);
    }

    public bool IsAllowed(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var normalized = email.Trim();
        lock (_gate)
        {
            return LoadUnlocked().Any(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    private List<string> LoadUnlocked()
    {
        if (!File.Exists(_path))
            return [];
        try
        {
            var json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return Normalize(list);
        }
        catch
        {
            return [];
        }
    }

    private static List<string> Normalize(IEnumerable<string> emails) =>
        emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Where(e => e.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
