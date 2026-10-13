using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace EcewsPortal.Services;

public interface IOtpService
{
    string CreateCode(string email);
    bool Verify(string email, string code);
    bool HasPending(string email);
}

public sealed class OtpService : IOtpService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, OtpEntry> _codes = new(StringComparer.OrdinalIgnoreCase);

    public string CreateCode(string email)
    {
        var key = Normalize(email);
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        _codes[key] = new OtpEntry(Hash(code), DateTimeOffset.UtcNow.Add(Ttl), Attempts: 0);
        return code;
    }

    public bool HasPending(string email)
    {
        var key = Normalize(email);
        return _codes.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow;
    }

    public bool Verify(string email, string code)
    {
        var key = Normalize(email);
        if (!_codes.TryGetValue(key, out var entry))
            return false;

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _codes.TryRemove(key, out _);
            return false;
        }

        if (entry.Attempts >= 5)
        {
            _codes.TryRemove(key, out _);
            return false;
        }

        var updated = entry with { Attempts = entry.Attempts + 1 };
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(updated.CodeHash),
                Encoding.UTF8.GetBytes(Hash(code.Trim()))))
        {
            _codes[key] = updated;
            return false;
        }

        _codes.TryRemove(key, out _);
        return true;
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    private sealed record OtpEntry(string CodeHash, DateTimeOffset ExpiresAt, int Attempts);
}
