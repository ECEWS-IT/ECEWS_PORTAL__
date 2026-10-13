using System.Security.Cryptography;
using System.Text;

namespace EcewsPortal.Services;

/// <summary>
/// Short-lived HMAC ticket so HireDesk trusts the portal session email.
/// HireDesk still emails an OTP; staff must enter the code before login.
/// </summary>
public static class HireDeskLaunchTicket
{
    public static string Create(string email, string secret, TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("HireDesk launch secret is not configured.");

        var normalised = email.Trim().ToLowerInvariant();
        var exp = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(2)).ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");
        var message = $"{normalised}|{exp}|{nonce}";
        var key = Encoding.UTF8.GetBytes(secret);
        var payload = Encoding.UTF8.GetBytes(message);
        var hash = HMACSHA256.HashData(key, payload);
        var sig = Convert.ToHexString(hash).ToLowerInvariant();
        var raw = $"{message}|{sig}";
        return Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
